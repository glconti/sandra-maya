using System.Text.Json;
using SandraMaya.Host.Playwright;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class WebExtractStructuredTool : IToolHandler
{
    private readonly IPlaywrightExecutionService _playwright;
    private readonly ILogger<WebExtractStructuredTool> _logger;

    public WebExtractStructuredTool(IPlaywrightExecutionService playwright, ILogger<WebExtractStructuredTool> logger)
    {
        _playwright = playwright;
        _logger = logger;
    }

    public string Name => "web_extract_structured";

    public string Description =>
        "Extract structured data from a web page using CSS selectors. Returns data as JSON.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The URL to navigate to."
                },
                "selectors": {
                    "type": "object",
                    "description": "Mapping of field names to CSS selectors, e.g. {\"title\": \"h1\", \"price\": \".price\"}.",
                    "additionalProperties": {
                        "type": "string"
                    }
                },
                "list_selector": {
                    "type": "string",
                    "description": "Optional CSS selector for repeating elements. When provided, extracts from each matching element creating an array of objects."
                }
            },
            "required": ["url", "selectors"],
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
        if (!arguments.TryGetProperty("selectors", out var selectorsProp) || selectorsProp.ValueKind != JsonValueKind.Object)
            return ToolResult.Error("The 'selectors' parameter is required and must be an object.");

        var url = urlProp.GetString()!;
        var listSelector = arguments.TryGetProperty("list_selector", out var ls) ? ls.GetString() : null;

        // Build the selectors map as a JS object literal
        var selectorEntries = new List<string>();
        foreach (var prop in selectorsProp.EnumerateObject())
        {
            var fieldName = JsonSerializer.Serialize(prop.Name);
            var cssSelector = JsonSerializer.Serialize(prop.Value.GetString() ?? string.Empty);
            selectorEntries.Add($"    {fieldName}: {cssSelector}");
        }
        var selectorsJs = "{\n" + string.Join(",\n", selectorEntries) + "\n  }";

        _logger.LogInformation("WebExtractStructured: extracting from {Url}", url);

        string extractionLogic;
        if (listSelector is not null)
        {
            extractionLogic = $$"""
                    const containers = await page.$$({{JsonEncode(listSelector)}});
                    const data = [];
                    for (const container of containers) {
                        const item = {};
                        for (const [field, sel] of Object.entries(selectors)) {
                            const el = await container.$(sel);
                            item[field] = el ? await el.innerText() : null;
                        }
                        data.push(item);
                    }
                    console.log(JSON.stringify({ data }));
            """;
        }
        else
        {
            extractionLogic = """
                    const data = {};
                    for (const [field, sel] of Object.entries(selectors)) {
                        const el = await page.$(sel);
                        data[field] = el ? await el.innerText() : null;
                    }
                    console.log(JSON.stringify({ data }));
            """;
        }

        var script = $$"""
            const { chromium } = require('playwright');
            (async () => {
                let browser;
                try {
                    browser = await chromium.launch({ headless: true });
                    const page = await browser.newPage();
                    await page.goto({{JsonEncode(url)}}, { waitUntil: 'domcontentloaded', timeout: 30000 });
                    const selectors = {{selectorsJs}};
            {{extractionLogic}}
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
            return ToolResult.Error($"Extraction failed on {url}: {Truncate(errorMsg)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            var data = doc.RootElement.GetProperty("data");
            var output = Truncate(data.GetRawText());
            return ToolResult.Success(output);
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
