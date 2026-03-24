namespace SandraMaya.Capabilities.Persistence;

public sealed record CapabilityStoreOptions(string RootPath, string FileName = "capability-registry.json")
{
    public string RegistryFilePath => Path.Combine(RootPath, FileName);
}
