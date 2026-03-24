using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Abstractions;

public interface ICapabilityRegistryService
{
    Task<CapabilityRecord> RegisterAsync(CapabilityRegistrationRequest request, CancellationToken cancellationToken = default);

    Task<CapabilityRecord?> GetAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CapabilityRecord>> ListAsync(CapabilityListOptions? options = null, CancellationToken cancellationToken = default);

    Task<CapabilityRecord> EnableAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task<CapabilityRecord> DisableAsync(string capabilityId, CancellationToken cancellationToken = default);

    Task<CapabilityRecord> RemoveAsync(string capabilityId, CancellationToken cancellationToken = default);
}
