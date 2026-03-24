using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Host.Assistant.ToolCalling;
using SandraMaya.Host.Assistant.ToolCalling.Tools;
using SandraMaya.Host.Jobs;
using SandraMaya.Host.Playwright;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Tests;

public sealed class PlaywrightScriptGenerationTests
{
    private static readonly ToolExecutionContext DefaultContext =
        new(Guid.NewGuid(), "test-session", null);

    [Fact]
    public async Task HostPlaywrightJobCrawlStrategy_CrawlAsync_UsesEsmPlaywrightImport()
    {
        var playwright = new RecordingPlaywrightExecutionService
        {
            ResultFactory = _ => new PlaywrightScriptResult
            {
                Succeeded = true,
                Output = "[]"
            }
        };

        var ingestion = new RecordingJobCrawlIngestionService();
        var strategy = new HostPlaywrightJobCrawlStrategy(
            playwright,
            ingestion,
            NullLogger<HostPlaywrightJobCrawlStrategy>.Instance);

        var site = new JobSiteDefinition
        {
            SiteKey = "jobs-ch",
            BaseUrl = "https://jobs.example.test",
            SearchUrl = "https://jobs.example.test/search",
            SupportedStrategies = [JobCrawlStrategyKind.PlaywrightBrowser]
        };

        var request = new JobCrawlRequest
        {
            UserProfileId = Guid.NewGuid(),
            SiteKey = site.SiteKey,
            Parameters = new Dictionary<string, string>
            {
                ["keywords"] = "teacher",
                ["location"] = "Zurich"
            }
        };

        var result = await strategy.CrawlAsync(site, request);

        Assert.Equal(JobCrawlRunStatus.Succeeded, result.Status);
        Assert.NotNull(playwright.LastRequest);
        AssertUsesEsmPlaywrightImport(playwright.LastRequest.Script);
    }

    [Fact]
    public async Task WebBrowseTool_ExecuteAsync_UsesEsmPlaywrightImport()
    {
        var playwright = new RecordingPlaywrightExecutionService
        {
            ResultFactory = _ => new PlaywrightScriptResult
            {
                Succeeded = true,
                Output = """{"text":"hello"}"""
            }
        };

        var tool = new WebBrowseTool(playwright, NullLogger<WebBrowseTool>.Instance);

        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new
            {
                url = "https://example.test/jobs"
            }),
            DefaultContext);

        Assert.False(result.IsError);
        Assert.Equal("hello", result.Content);
        Assert.NotNull(playwright.LastRequest);
        AssertUsesEsmPlaywrightImport(playwright.LastRequest.Script);
    }

    [Fact]
    public async Task WebSearchTool_ExecuteAsync_UsesEsmPlaywrightImport()
    {
        var playwright = new RecordingPlaywrightExecutionService
        {
            ResultFactory = _ => new PlaywrightScriptResult
            {
                Succeeded = true,
                Output = """{"results":["first result"]}"""
            }
        };

        var tool = new WebSearchTool(playwright, NullLogger<WebSearchTool>.Instance);

        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new
            {
                url = "https://example.test/search",
                search_input_selector = "#search",
                search_text = "teacher",
                results_selector = ".result"
            }),
            DefaultContext);

        Assert.False(result.IsError);
        Assert.Contains("first result", result.Content, StringComparison.Ordinal);
        Assert.NotNull(playwright.LastRequest);
        AssertUsesEsmPlaywrightImport(playwright.LastRequest.Script);
    }

    [Fact]
    public async Task WebExtractStructuredTool_ExecuteAsync_UsesEsmPlaywrightImport()
    {
        var playwright = new RecordingPlaywrightExecutionService
        {
            ResultFactory = _ => new PlaywrightScriptResult
            {
                Succeeded = true,
                Output = """{"data":{"title":"Role"}}"""
            }
        };

        var tool = new WebExtractStructuredTool(playwright, NullLogger<WebExtractStructuredTool>.Instance);

        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new
            {
                url = "https://example.test/job/1",
                selectors = new Dictionary<string, string>
                {
                    ["title"] = "h1"
                }
            }),
            DefaultContext);

        Assert.False(result.IsError);
        Assert.Contains("Role", result.Content, StringComparison.Ordinal);
        Assert.NotNull(playwright.LastRequest);
        AssertUsesEsmPlaywrightImport(playwright.LastRequest.Script);
    }

    [Fact]
    public async Task WebScreenshotTool_ExecuteAsync_UsesEsmPlaywrightImport()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sandra-maya-tests-{Guid.NewGuid():N}");
        var storage = new StorageLayout(
            Root: tempRoot,
            SqlitePath: Path.Combine(tempRoot, "sqlite", "db.sqlite"),
            UploadsPath: Path.Combine(tempRoot, "files"),
            CapabilitiesPath: Path.Combine(tempRoot, "capabilities"),
            GeneratedCapabilitiesPath: Path.Combine(tempRoot, "capabilities", "generated"),
            WorkPath: Path.Combine(tempRoot, "work"),
            TempPath: Path.Combine(tempRoot, "tmp"));

        var playwright = new RecordingPlaywrightExecutionService
        {
            ResultFactory = request =>
            {
                var screenshotPath = ExtractJsonStringLiteral(
                    request.Script,
                    "console.log(JSON.stringify({ path:");

                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                File.WriteAllBytes(screenshotPath, [1, 2, 3]);

                return new PlaywrightScriptResult
                {
                    Succeeded = true,
                    Output = "{}"
                };
            }
        };

        try
        {
            var tool = new WebScreenshotTool(playwright, storage, NullLogger<WebScreenshotTool>.Instance);

            var result = await tool.ExecuteAsync(
                JsonSerializer.SerializeToElement(new
                {
                    url = "https://example.test/job/1"
                }),
                DefaultContext);

            Assert.False(result.IsError);
            Assert.Contains("Screenshot saved to", result.Content, StringComparison.Ordinal);
            Assert.NotNull(playwright.LastRequest);
            AssertUsesEsmPlaywrightImport(playwright.LastRequest.Script);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void AssertUsesEsmPlaywrightImport(string script)
    {
        Assert.Contains("import { chromium } from 'playwright';", script, StringComparison.Ordinal);
        Assert.DoesNotContain("require('playwright')", script, StringComparison.Ordinal);
    }

    private static string ExtractJsonStringLiteral(string script, string marker)
    {
        var markerIndex = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Expected marker '{marker}' in generated script.");

        var literalStart = script.IndexOf('"', markerIndex + marker.Length);
        Assert.True(literalStart >= 0, "Expected JSON string literal in generated script.");

        var index = literalStart + 1;
        var escaped = false;

        for (; index < script.Length; index++)
        {
            var current = script[index];

            if (current == '"' && !escaped)
            {
                break;
            }

            escaped = current == '\\' && !escaped;
            if (current != '\\')
            {
                escaped = false;
            }
        }

        Assert.True(index < script.Length, "Generated script contained an unterminated JSON string literal.");

        var jsonLiteral = script.Substring(literalStart, index - literalStart + 1);
        return JsonSerializer.Deserialize<string>(jsonLiteral)!;
    }

    private sealed class RecordingPlaywrightExecutionService : IPlaywrightExecutionService
    {
        public Func<PlaywrightScriptRequest, PlaywrightScriptResult>? ResultFactory { get; init; }

        public PlaywrightScriptRequest? LastRequest { get; private set; }

        public Task<PlaywrightScriptResult> ExecuteScriptAsync(
            PlaywrightScriptRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(ResultFactory?.Invoke(request) ?? new PlaywrightScriptResult
            {
                Succeeded = true,
                Output = "{}"
            });
        }
    }

    private sealed class RecordingJobCrawlIngestionService : IJobCrawlIngestionService
    {
        public Task<JobCrawlResult> ImportAsync(
            JobCrawlDiscoveryBatch batch,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JobCrawlResult
            {
                Request = batch.Request,
                Status = JobCrawlRunStatus.Succeeded,
                StartedAtUtc = batch.StartedAtUtc,
                CompletedAtUtc = batch.CompletedAtUtc ?? DateTimeOffset.UtcNow,
                StrategyKind = batch.StrategyKind,
                DiscoveredCount = batch.Jobs.Count,
                IngestedCount = batch.Jobs.Count,
                Items = []
            });
        }
    }
}
