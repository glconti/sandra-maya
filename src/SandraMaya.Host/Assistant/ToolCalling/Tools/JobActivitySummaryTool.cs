using System.Text.Json;
using SandraMaya.Application.Abstractions;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobActivitySummaryTool(IJobActivityReportingService reportingService) : IToolHandler
{
    public string Name => "job_activity_summary";

    public string Description =>
        "Get a summary report of job search activity for the current week or month";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["weekly", "monthly"],
                    "description": "The reporting period — weekly or monthly"
                }
            },
            "required": ["period"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var period = arguments.GetProperty("period").GetString()!.ToLowerInvariant();

            var summary = period switch
            {
                "weekly" => await reportingService.GetWeeklySummaryAsync(context.UserId, cancellationToken: cancellationToken),
                "monthly" => await reportingService.GetMonthlySummaryAsync(context.UserId, cancellationToken: cancellationToken),
                _ => throw new ArgumentException($"Invalid period: '{period}'. Must be 'weekly' or 'monthly'.")
            };

            return ToolResult.Json(new
            {
                summary.Period,
                summary.RangeStartUtc,
                summary.RangeEndUtc,
                summary.JobsDiscovered,
                summary.JobsTracked,
                summary.ApplicationsSubmitted,
                summary.InterviewsAdvanced,
                summary.OffersReceived,
                summary.RejectionsLogged,
                summary.WithdrawalsLogged,
                summary.CurrentStatusCounts,
                summary.RecentDiscoveries,
                summary.RecentApplicationUpdates
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to get activity summary: {ex.Message}");
        }
    }
}
