using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobTrackApplicationTool(IJobApplicationTrackingService trackingService) : IToolHandler
{
    public string Name => "job_track_application";

    public string Description =>
        "Update the status of a job application (e.g. Applied, Interviewing, Offer, Rejected)";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "job_posting_id": { "type": "string", "format": "uuid", "description": "The ID of the job posting" },
                "status": {
                    "type": "string",
                    "enum": ["Draft", "Saved", "Applied", "Interviewing", "Offer", "Rejected", "Withdrawn", "Archived", "Interested"],
                    "description": "The new application status"
                },
                "notes": { "type": "string", "description": "Optional notes about this status change" }
            },
            "required": ["job_posting_id", "status"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobPostingIdStr = arguments.GetProperty("job_posting_id").GetString()!;
            if (!Guid.TryParse(jobPostingIdStr, out var jobPostingId))
                return ToolResult.Error($"Invalid job_posting_id: '{jobPostingIdStr}' is not a valid GUID.");

            var statusStr = arguments.GetProperty("status").GetString()!;
            if (!Enum.TryParse<JobApplicationStatus>(statusStr, ignoreCase: true, out var status))
                return ToolResult.Error($"Invalid status: '{statusStr}'. Valid values: {string.Join(", ", Enum.GetNames<JobApplicationStatus>())}");

            var notes = arguments.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";

            var request = new JobApplicationStatusUpdateRequest(
                UserProfileId: context.UserId,
                JobPostingId: jobPostingId,
                Status: status,
                NotesMarkdown: notes);

            var state = await trackingService.MarkStatusAsync(request, cancellationToken);

            return ToolResult.Json(new
            {
                JobPostingId = jobPostingId,
                JobTitle = state.JobPosting.Title,
                Company = state.JobPosting.CompanyName,
                CurrentStatus = state.CurrentStatus?.ToString()
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to update application status: {ex.Message}");
        }
    }
}
