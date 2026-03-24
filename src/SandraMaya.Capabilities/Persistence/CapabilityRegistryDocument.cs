using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Persistence;

internal sealed class CapabilityRegistryDocument
{
    public List<CapabilityRecord> Capabilities { get; init; } = [];

    public List<CapabilityInstallProvenanceRecord> Installations { get; init; } = [];

    public List<CapabilityExecutionRecord> Executions { get; init; } = [];
}
