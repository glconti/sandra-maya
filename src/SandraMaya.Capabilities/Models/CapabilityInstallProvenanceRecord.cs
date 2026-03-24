namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityInstallProvenanceRecord(
    string InstallId,
    string CapabilityId,
    DateTimeOffset InstalledAt,
    string InstalledBy,
    CapabilitySourceDescriptor Source,
    string InstallPath,
    CapabilityPermission[]? PermissionSnapshot = null,
    string? PackageDigest = null,
    string[]? ValidationSteps = null,
    string? Notes = null)
{
    public CapabilityPermission[] PermissionSnapshot { get; init; } = PermissionSnapshot ?? [];
    public string[] ValidationSteps { get; init; } = ValidationSteps ?? [];
}
