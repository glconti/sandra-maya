namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityRecord(
    string Id,
    string Name,
    string Version,
    CapabilitySourceDescriptor Source,
    CapabilityRuntimeDescriptor Runtime,
    string InstallPath,
    CapabilityStatus Status,
    CapabilityState State,
    CapabilityPermissionSet Permissions,
    CapabilityHealthSnapshot Health,
    CapabilityTimestamps Timestamps,
    string? Description = null);
