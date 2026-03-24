using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobListApplicationsTool(IJobApplicationTrackingService trackingService) : IToolHandler
{
    public string Name => "job_list_applications";

    public string Description => "List all tracked job applications with their current status";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "status_filter": {
                    "type": "array",
                    "items": {
                        "type": "string",
                        "enum": ["Draft", "Saved", "Applied", "Interviewing", "Offer", "Rejected", "Withdrawn", "Archived", "Interested"]
                    },
                    "description": "Filter by specific application statuses"
                },
                "limit": { "type": "integer", "description": "Maximum number of results to return", "default": 50 }
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
            IReadOnlyCollection<JobApplicationStatus>? statuses = null;
            if (arguments.TryGetProperty("status_filter", out var sf) && sf.ValueKind == JsonValueKind.Array)
            {
                var parsed = new List<JobApplicationStatus>();
                foreach (var item in sf.EnumerateArray())
                {
                    if (Enum.TryParse<JobApplicationStatus>(item.GetString(), ignoreCase: true, out var s))
                        parsed.Add(s);
                }
                if (parsed.Count > 0)
                    statuses = parsed;
            }

            var limit = arguments.TryGetProperty("limit", out var lim) ? lim.GetInt32() : 50;

            var query = new JobApplicationListQuery(Statuses: statuses, Limit: limit);
            var applications = await trackingService.ListApplicationsAsync(context.UserId, query, cancellationToken);

            var output = applications.Select(a => new
            {
                JobPostingId = a.JobPosting.Id,
                JobTitle = a.JobPosting.Title,
                Company = a.JobPosting.CompanyName,
                Location = a.JobPosting.Location,
                CurrentStatus = a.CurrentStatus?.ToString(),
                AppliedAt = a.StatusRecord?.AppliedAtUtc,
                UpdatedAt = a.StatusRecord?.UpdatedAtUtc
            }).ToList();

            return ToolResult.Json(output);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to list applications: {ex.Message}");
        }
    }
}
