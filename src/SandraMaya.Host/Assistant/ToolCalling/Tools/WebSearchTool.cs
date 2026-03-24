using System.Text.Json;
using SandraMaya.Host.Playwright;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class WebSearchTool : IToolHandler
{
    private readonly IPlaywrightExecutionService _playwright;
    private readonly ILogger<WebSearchTool> _logger;

    public WebSearchTool(IPlaywrightExecutionService playwright, ILogger<WebSearchTool> logger)
    {
        _playwright = playwright;
        _logger = logger;
    }

    public string Name => "web_search";

    public string Description =>
        "Search a website by filling its search form and extracting results. " +
        "Useful for searching job boards or any website with a search feature.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The search page URL to navigate to."
                },
                "search_input_selector": {
                    "type": "string",
                    "description": "CSS selector for the search input field."
                },
                "search_text": {
                    "type": "string",
                    "description": "The text to type into the search input."
                },
                "submit_selector": {
                    "type": "string",
                    "description": "Optional CSS selector for the submit button. If omitted, Enter is pressed instead."
                },
                "results_selector": {
                    "type": "string",
                    "description": "CSS selector for the result item elements."
                },
                "max_results": {
                    "type": "integer",
                    "description": "Maximum number of results to extract. Defaults to 20.",
                    "default": 20
                }
            },
            "required": ["url", "search_input_selector", "search_text", "results_selector"],
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
        if (!arguments.TryGetProperty("search_input_selector", out var inputProp) || string.IsNullOrWhiteSpace(inputProp.GetString()))
            return ToolResult.Error("The 'search_input_selector' parameter is required.");
        if (!arguments.TryGetProperty("search_text", out var textProp) || string.IsNullOrWhiteSpace(textProp.GetString()))
            return ToolResult.Error("The 'search_text' parameter is required.");
        if (!arguments.TryGetProperty("results_selector", out var resultsProp) || string.IsNullOrWhiteSpace(resultsProp.GetString()))
            return ToolResult.Error("The 'results_selector' parameter is required.");

        var url = urlProp.GetString()!;
        var inputSelector = inputProp.GetString()!;
        var searchText = textProp.GetString()!;
        var resultsSelector = resultsProp.GetString()!;
        var submitSelector = arguments.TryGetProperty("submit_selector", out var sp) ? sp.GetString() : null;
        var maxResults = arguments.TryGetProperty("max_results", out var mr) ? mr.GetInt32() : 20;

        _logger.LogInformation("WebSearch: searching {Url} for '{SearchText}'", url, searchText);

        var submitAction = submitSelector is not null
            ? $"await page.click({JsonEncode(submitSelector)});"
            : $"await page.press({JsonEncode(inputSelector)}, 'Enter');";

        var script = $$"""
            const { chromium } = require('playwright');
            (async () => {
                let browser;
                try {
                    browser = await chromium.launch({ headless: true });
                    const page = await browser.newPage();
                    await page.goto({{JsonEncode(url)}}, { waitUntil: 'domcontentloaded', timeout: 30000 });
                    await page.fill({{JsonEncode(inputSelector)}}, {{JsonEncode(searchText)}});
                    {{submitAction}}
                    await page.waitForSelector({{JsonEncode(resultsSelector)}}, { timeout: 15000 });
                    await page.waitForTimeout(1000);
                    const items = await page.$$eval(
                        {{JsonEncode(resultsSelector)}},
                        (els, max) => els.slice(0, max).map(el => el.innerText.trim()),
                        {{maxResults}}
                    );
                    console.log(JSON.stringify({ results: items }));
                } catch (err) {
                    console.error(err.message);
                    process.exitCode = 1;
                } finally {
                    if (browser) await browser.close();
                }
            })();
            """;

        var result = await _playwright.ExecuteScriptAsync(
            new PlaywrightScriptRequest { Script = script, Timeout = TimeSpan.FromSeconds(90) },
            cancellationToken);

        if (!result.Succeeded)
        {
            var errorMsg = string.IsNullOrWhiteSpace(result.ErrorOutput)
                ? "Playwright script failed with no error output."
                : result.ErrorOutput;
            return ToolResult.Error($"Search failed on {url}: {Truncate(errorMsg)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(result.Output);
            var results = doc.RootElement.GetProperty("results");
            var output = Truncate(results.GetRawText());
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
