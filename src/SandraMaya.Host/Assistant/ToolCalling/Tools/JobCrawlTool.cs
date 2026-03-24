using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobCrawlTool(IJobCrawler crawler) : IToolHandler
{
    public string Name => "job_crawl";

    public string Description =>
        "Trigger a live crawl of a Swiss job site to discover new job postings. Use job_list_sites to see available sites.";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "site_key": { "type": "string", "description": "The site key to crawl (e.g. jobs-ch)" },
                "keywords": { "type": "string", "description": "Search keywords for the crawl" },
                "location": { "type": "string", "description": "Location filter for the crawl", "default": "Zürich" }
            },
            "required": ["site_key"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteKey = arguments.GetProperty("site_key").GetString()!;
            var keywords = arguments.TryGetProperty("keywords", out var kw) ? kw.GetString() : null;
            var location = arguments.TryGetProperty("location", out var loc) ? loc.GetString() : "Zürich";

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(keywords))
                parameters["keywords"] = keywords;
            if (!string.IsNullOrWhiteSpace(location))
                parameters["location"] = location;

            var request = new JobCrawlRequest
            {
                UserProfileId = context.UserId,
                SiteKey = siteKey,
                Trigger = JobCrawlTriggerKind.Manual,
                RequestedAtUtc = DateTimeOffset.UtcNow,
                Parameters = parameters
            };

            var result = await crawler.CrawlAsync(request, cancellationToken);

            return ToolResult.Json(new
            {
                result.Status,
                result.DiscoveredCount,
                result.IngestedCount,
                result.UpdatedCount,
                result.FailedCount,
                result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to crawl job site: {ex.Message}");
        }
    }
}
