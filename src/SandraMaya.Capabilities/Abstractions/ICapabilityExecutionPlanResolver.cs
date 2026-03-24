using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Abstractions;

public interface ICapabilityExecutionPlanResolver
{
    Task<CapabilityExecutionPlan> ResolveAsync(string capabilityId, CancellationToken cancellationToken = default);
}
