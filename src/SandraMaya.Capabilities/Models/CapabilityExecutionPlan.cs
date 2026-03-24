namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityExecutionPlan(
    string CapabilityId,
    string Name,
    string Version,
    CapabilityKind Kind,
    string Command,
    string WorkingDirectory,
    string ContainmentProfile,
    CapabilityPermission[] DeclaredPermissions,
    bool RequiresApproval,
    DateTimeOffset ResolvedAt,
    string[]? Arguments = null)
{
    public string[] Arguments { get; init; } = Arguments ?? [];
}
