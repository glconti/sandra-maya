using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobSearchSavedTool(IMemoryQueryService memoryQuery) : IToolHandler
{
    public string Name => "job_search_saved";

    public string Description =>
        "Search previously discovered and saved job postings by keyword, location, or source site";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "search_text": { "type": "string", "description": "Keywords to search for in job titles and descriptions" },
                "source_site": { "type": "string", "description": "Filter by source site key (e.g. jobs-ch)" },
                "location": { "type": "string", "description": "Filter by job location" },
                "active_only": { "type": "boolean", "description": "Only return active postings", "default": true },
                "limit": { "type": "integer", "description": "Maximum number of results to return", "default": 20 }
            },
            "required": []
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchText = arguments.TryGetProperty("search_text", out var st) ? st.GetString() : null;
            var sourceSite = arguments.TryGetProperty("source_site", out var ss) ? ss.GetString() : null;
            var location = arguments.TryGetProperty("location", out var loc) ? loc.GetString() : null;
            var activeOnly = arguments.TryGetProperty("active_only", out var ao) ? ao.GetBoolean() : true;
            var limit = arguments.TryGetProperty("limit", out var lim) ? lim.GetInt32() : 20;

            var query = new JobPostingQuery(
                SearchText: searchText,
                SourceSite: sourceSite,
                Location: location,
                ActiveOnly: activeOnly,
                Limit: limit);

            var results = await memoryQuery.SearchJobPostingsAsync(context.UserId, query, cancellationToken);

            var output = results.Select(j => new
            {
                j.Id,
                j.Title,
                j.CompanyName,
                j.Location,
                Url = j.SourceUrl,
                j.SourceSite
            }).ToList();

            return ToolResult.Json(output);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to search job postings: {ex.Message}");
        }
    }
}
