namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityExecutionRecord(
    string ExecutionId,
    string CapabilityId,
    DateTimeOffset RequestedAt,
    CapabilityExecutionStatus Status,
    string RequestedBy,
    string Command,
    string WorkingDirectory,
    string[]? Arguments = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    string? CorrelationId = null,
    string? ContainmentProfile = null,
    int? ExitCode = null,
    string? ErrorMessage = null)
{
    public string[] Arguments { get; init; } = Arguments ?? [];
}
