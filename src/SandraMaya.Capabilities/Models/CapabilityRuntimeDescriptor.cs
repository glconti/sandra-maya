namespace SandraMaya.Capabilities.Models;

public sealed record CapabilityRuntimeDescriptor(
    CapabilityLanguage Language,
    CapabilityRuntime Runtime,
    string EntryPoint,
    string? RuntimeVersion = null,
    string? WorkingDirectory = null,
    string[]? Arguments = null)
{
    public string[] Arguments { get; init; } = Arguments ?? [];
}
