using System.Text.Json;
using SandraMaya.Application.Abstractions;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobListSitesTool(IJobSiteRegistry siteRegistry) : IToolHandler
{
    public string Name => "job_list_sites";

    public string Description => "List available job sites that can be crawled for job postings";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """);

    public Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sites = siteRegistry.GetAll()
                .Select(s => new
                {
                    s.SiteKey,
                    s.DisplayName,
                    s.BaseUrl
                })
                .ToList();

            return Task.FromResult(ToolResult.Json(sites));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to list job sites: {ex.Message}"));
        }
    }
}
