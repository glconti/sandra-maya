using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Services;

public sealed class CapabilityActivityService(ICapabilityStore store) : ICapabilityActivityService
{
    private readonly ICapabilityStore _store = store;

    public async Task<CapabilityInstallProvenanceRecord> RecordInstallationAsync(
        CapabilityInstallProvenanceRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var capability = await GetRequiredCapabilityAsync(record.CapabilityId, cancellationToken).ConfigureAwait(false);
        EnsureNotRemoved(capability);

        await _store.AddInstallRecordAsync(record, cancellationToken).ConfigureAwait(false);

        var installedState = capability.Status == CapabilityStatus.Enabled ? CapabilityState.Ready : CapabilityState.Installed;
        var updatedCapability = capability with
        {
            State = installedState,
            Timestamps = capability.Timestamps with
            {
                UpdatedAt = record.InstalledAt,
                InstalledAt = record.InstalledAt
            }
        };

        await _store.UpsertCapabilityAsync(updatedCapability, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public Task<IReadOnlyList<CapabilityInstallProvenanceRecord>> GetInstallationHistoryAsync(
        string capabilityId,
        CancellationToken cancellationToken = default) =>
        _store.ListInstallRecordsAsync(capabilityId, cancellationToken);

    public async Task<CapabilityExecutionRecord> RecordExecutionAsync(
        CapabilityExecutionRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var capability = await GetRequiredCapabilityAsync(record.CapabilityId, cancellationToken).ConfigureAwait(false);
        EnsureNotRemoved(capability);

        await _store.AddExecutionRecordAsync(record, cancellationToken).ConfigureAwait(false);

        var updateAt = record.CompletedAt ?? record.StartedAt ?? record.RequestedAt;
        var updatedCapability = capability with
        {
            State = DetermineState(capability, record.Status),
            Health = UpdateHealth(capability.Health, record),
            Timestamps = capability.Timestamps with
            {
                UpdatedAt = updateAt,
                LastExecutedAt = updateAt
            }
        };

        await _store.UpsertCapabilityAsync(updatedCapability, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public Task<IReadOnlyList<CapabilityExecutionRecord>> GetExecutionHistoryAsync(
        string capabilityId,
        CancellationToken cancellationToken = default) =>
        _store.ListExecutionRecordsAsync(capabilityId, cancellationToken);

    private async Task<CapabilityRecord> GetRequiredCapabilityAsync(string capabilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("Capability id is required.", nameof(capabilityId));
        }

        var capability = await _store.GetCapabilityAsync(capabilityId.Trim(), cancellationToken).ConfigureAwait(false);
        return capability ?? throw new CapabilityGovernanceException($"Capability '{capabilityId}' was not found.");
    }

    private static CapabilityState DetermineState(CapabilityRecord capability, CapabilityExecutionStatus status) =>
        status switch
        {
            CapabilityExecutionStatus.Failed => CapabilityState.Faulted,
            CapabilityExecutionStatus.Succeeded when capability.Status == CapabilityStatus.Enabled => CapabilityState.Ready,
            CapabilityExecutionStatus.Succeeded => CapabilityState.Installed,
            _ => capability.State
        };

    private static CapabilityHealthSnapshot UpdateHealth(CapabilityHealthSnapshot current, CapabilityExecutionRecord record)
    {
        var observedAt = record.CompletedAt ?? record.StartedAt ?? record.RequestedAt;

        return record.Status switch
        {
            CapabilityExecutionStatus.Succeeded => current with
            {
                Status = CapabilityHealthStatus.Healthy,
                CheckedAt = observedAt,
                LastSuccessAt = observedAt,
                Message = null
            },
            CapabilityExecutionStatus.Failed => current with
            {
                Status = CapabilityHealthStatus.Unhealthy,
                CheckedAt = observedAt,
                LastFailureAt = observedAt,
                Message = record.ErrorMessage
            },
            CapabilityExecutionStatus.Cancelled => current with
            {
                Status = CapabilityHealthStatus.Degraded,
                CheckedAt = observedAt,
                Message = "Execution cancelled."
            },
            _ => current with
            {
                CheckedAt = observedAt
            }
        };
    }

    private static void EnsureNotRemoved(CapabilityRecord capability)
    {
        if (capability.Status == CapabilityStatus.Removed || capability.State == CapabilityState.Removed)
        {
            throw new CapabilityGovernanceException($"Capability '{capability.Id}' has been removed.");
        }
    }
}
