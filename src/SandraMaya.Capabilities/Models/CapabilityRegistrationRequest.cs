namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityRegistrationRequest(
    string Id,
    string Name,
    string Version,
    CapabilitySourceDescriptor Source,
    CapabilityRuntimeDescriptor Runtime,
    string InstallPath,
    CapabilityPermissionSet Permissions,
    string? Description = null,
    CapabilityStatus InitialStatus = CapabilityStatus.Disabled,
    CapabilityState InitialState = CapabilityState.PendingInstall,
    CapabilityHealthSnapshot? InitialHealth = null)
{
    public static CapabilityRegistrationRequest CreateBuiltIn(
        string id,
        string name,
        string version,
        CapabilityRuntimeDescriptor runtime,
        string installPath,
        CapabilityPermissionSet permissions,
        string? description = null,
        CapabilityStatus initialStatus = CapabilityStatus.Enabled,
        CapabilityState initialState = CapabilityState.Ready) =>
        new(
            id,
            name,
            version,
            new CapabilitySourceDescriptor(CapabilityKind.BuiltIn, CapabilitySourceType.Embedded, installPath),
            runtime,
            installPath,
            permissions,
            description,
            initialStatus,
            initialState);

    public static CapabilityRegistrationRequest CreateGenerated(
        string id,
        string name,
        string version,
        CapabilityRuntimeDescriptor runtime,
        string installPath,
        CapabilityPermissionSet permissions,
        string reference,
        string? createdBy = null,
        string? description = null,
        CapabilityStatus initialStatus = CapabilityStatus.Disabled,
        CapabilityState initialState = CapabilityState.Installed) =>
        new(
            id,
            name,
            version,
            new CapabilitySourceDescriptor(CapabilityKind.Generated, CapabilitySourceType.GeneratedArtifact, reference, CreatedBy: createdBy),
            runtime,
            installPath,
            permissions,
            description,
            initialStatus,
            initialState);
}
