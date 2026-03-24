using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Abstractions;

public interface ICapabilityActivityService
{
    Task<CapabilityInstallProvenanceRecord> RecordInstallationAsync(CapabilityInstallProvenanceRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityInstallProvenanceRecord>> GetInstallationHistoryAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task<CapabilityExecutionRecord> RecordExecutionAsync(CapabilityExecutionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityExecutionRecord>> GetExecutionHistoryAsync(string capabilityId, CancellationToken cancellationToken = default);
}
