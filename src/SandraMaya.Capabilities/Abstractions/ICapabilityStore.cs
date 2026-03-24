using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Abstractions;

public interface ICapabilityStore
{
    Task<CapabilityRecord?> GetCapabilityAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityRecord>> ListCapabilitiesAsync(CancellationToken cancellationToken = default);

    Task UpsertCapabilityAsync(CapabilityRecord capability, CancellationToken cancellationToken = default);

    Task AddInstallRecordAsync(CapabilityInstallProvenanceRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityInstallProvenanceRecord>> ListInstallRecordsAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task AddExecutionRecordAsync(CapabilityExecutionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityExecutionRecord>> ListExecutionRecordsAsync(string capabilityId, CancellationToken cancellationToken = default);
}
