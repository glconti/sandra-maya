using System.Text.Json;
using SandraMaya.Host.Playwright;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class WebBrowseTool : IToolHandler
{
    private readonly IPlaywrightExecutionService _playwright;
    private readonly ILogger<WebBrowseTool> _logger;

    public WebBrowseTool(IPlaywrightExecutionService playwright, ILogger<WebBrowseTool> logger)
    {
        _playwright = playwright;
        _logger = logger;
    }

    public string Name => "web_browse";

    public string Description =>
        "Navigate to a URL and extract the page's text content. " +
        "Use this to read web pages, check job listings, or research information online.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The URL to navigate to."
                },
                "wait_for_selector": {
                    "type": "string",
                    "description": "Optional CSS selector to wait for before extracting content."
                },
                "extract_selector": {
                    "type": "string",
                    "description": "Optional CSS selector to extract text from a specific element instead of the full page."
                }
            },
            "required": ["url"],
            "additionalProperties": false
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("url", out var urlProp) || string.IsNullOrWhiteSpace(urlProp.GetString()))
            return ToolResult.Error("The 'url' parameter is required.");

        var url = urlProp.GetString()!;
        var waitForSelector = arguments.TryGetProperty("wait_for_selector", out var wfs) ? wfs.GetString() : null;
        var extractSelector = arguments.TryGetProperty("extract_selector", out var es) ? es.GetString() : null;

        _logger.LogInformation("WebBrowse: navigating to {Url}", url);

        var waitLine = waitForSelector is not null
            ? $"    await page.waitForSelector({JsonEncode(waitForSelector)}, {{ timeout: 10000 }});"
            : string.Empty;

        var extractLine = extractSelector is not null
            ? $"await page.textContent({JsonEncode(extractSelector)})"
            : "await page.evaluate(() => document.body.innerText)";

        var script = $$"""
            import { chromium } from 'playwright';
            (async () => {
                let browser;
                try {
                    browser = await chromium.launch({ headless: true });
                    const page = await browser.newPage();
                    await page.goto({{JsonEncode(url)}}, { waitUntil: 'domcontentloaded', timeout: 30000 });
            {{waitLine}}
                    const text = {{extractLine}};
                    const output = (text || '').substring(0, 15000);
                    console.log(JSON.stringify({ text: output }));
                } catch (err) {
                    console.error(err.message);
                    process.exitCode = 1;
                } finally {
                    if (browser) await browser.close();
                }
            })();
            """;

        var result = await _playwright.ExecuteScriptAsync(
            new PlaywrightScriptRequest { Script = script },
            cancellationToken);

        if (!result.Succeeded)
        {
            var errorMsg = string.IsNullOrWhiteSpace(result.ErrorOutput)
                ? "Playwright script failed with no error output."
                : result.ErrorOutput;
            return ToolResult.Error($"Failed to browse {url}: {Truncate(errorMsg)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            return ToolResult.Success(text);
        }
        catch
        {
            return ToolResult.Success(Truncate(result.Output));
        }
    }

    private static string JsonEncode(string value) =>
        JsonSerializer.Serialize(value);

    private static string Truncate(string text, int maxLength = 15000) =>
        text.Length <= maxLength ? text : text[..maxLength] + "\n...[truncated]";
}
