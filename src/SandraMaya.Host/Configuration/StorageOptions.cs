namespace SandraMaya.Host.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Root { get; init; } = "App_Data";

    public string? SqlitePath { get; init; }

    public string? UploadsPath { get; init; }

    public string? CapabilitiesPath { get; init; }

    public string? GeneratedCapabilitiesPath { get; init; }

    public string? WorkPath { get; init; }

    public string? TempPath { get; init; }

    public string CapabilityRegistryFileName { get; init; } = "capability-registry.json";
}
