using System.Text.Json;
using SandraMaya.Host.Playwright;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class WebScreenshotTool : IToolHandler
{
    private readonly IPlaywrightExecutionService _playwright;
    private readonly StorageLayout _storage;
    private readonly ILogger<WebScreenshotTool> _logger;

    public WebScreenshotTool(
        IPlaywrightExecutionService playwright,
        StorageLayout storage,
        ILogger<WebScreenshotTool> logger)
    {
        _playwright = playwright;
        _storage = storage;
        _logger = logger;
    }

    public string Name => "web_screenshot";

    public string Description =>
        "Take a screenshot of a web page. Useful for capturing visual content " +
        "like job postings, maps, or complex layouts.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The URL to navigate to."
                },
                "full_page": {
                    "type": "boolean",
                    "description": "Whether to capture the full scrollable page. Defaults to false.",
                    "default": false
                },
                "selector": {
                    "type": "string",
                    "description": "Optional CSS selector to screenshot a specific element instead of the viewport."
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
        var fullPage = arguments.TryGetProperty("full_page", out var fp) && fp.GetBoolean();
        var selector = arguments.TryGetProperty("selector", out var sp) ? sp.GetString() : null;

        Directory.CreateDirectory(_storage.TempPath);
        var screenshotFileName = $"screenshot-{Guid.NewGuid():N}.png";
        var screenshotPath = Path.Combine(_storage.TempPath, screenshotFileName);

        _logger.LogInformation("WebScreenshot: capturing {Url}", url);

        string captureLogic;
        if (selector is not null)
        {
            captureLogic = $$"""
                    const element = await page.waitForSelector({{JsonEncode(selector)}}, { timeout: 10000 });
                    await element.screenshot({ path: {{JsonEncode(screenshotPath)}} });
            """;
        }
        else
        {
            captureLogic = $$"""
                    await page.screenshot({
                        path: {{JsonEncode(screenshotPath)}},
                        fullPage: {{(fullPage ? "true" : "false")}}
                    });
            """;
        }

        var script = $$"""
            import { chromium } from 'playwright';
            (async () => {
                let browser;
                try {
                    browser = await chromium.launch({ headless: true });
                    const page = await browser.newPage();
                    await page.goto({{JsonEncode(url)}}, { waitUntil: 'domcontentloaded', timeout: 30000 });
                    await page.waitForTimeout(1000);
            {{captureLogic}}
                    console.log(JSON.stringify({ path: {{JsonEncode(screenshotPath)}} }));
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
            return ToolResult.Error($"Screenshot failed for {url}: {Truncate(errorMsg)}");
        }

        if (!File.Exists(screenshotPath))
            return ToolResult.Error("Screenshot was not saved. The Playwright script may have failed silently.");

        return ToolResult.Success(
            $"Screenshot saved to {screenshotPath}. " +
            "The image can be sent as a Telegram attachment.");
    }

    private static string JsonEncode(string value) =>
        JsonSerializer.Serialize(value);

    private static string Truncate(string text, int maxLength = 2000) =>
        text.Length <= maxLength ? text : text[..maxLength] + "\n...[truncated]";
}
