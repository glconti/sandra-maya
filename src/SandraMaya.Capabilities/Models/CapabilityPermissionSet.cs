namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityPermissionSet(
    CapabilityPermission[]? Scopes = null,
    bool RequiresExplicitApproval = false,
    string? ContainmentBoundary = null,
    string? Notes = null)
{
    public CapabilityPermission[] Scopes { get; init; } = Scopes ?? [];
}
