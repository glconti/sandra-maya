using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Services;

public sealed class CapabilityRegistryService(ICapabilityStore store, TimeProvider? timeProvider = null) : ICapabilityRegistryService
{
    private readonly ICapabilityStore _store = store;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CapabilityRecord> RegisterAsync(CapabilityRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRegistrationRequest(request);

        var existing = await _store.GetCapabilityAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new CapabilityGovernanceException($"Capability '{request.Id}' is already registered.");
        }

        var now = _timeProvider.GetUtcNow();
        var health = request.InitialHealth ?? new CapabilityHealthSnapshot(CapabilityHealthStatus.Unknown);
        var timestamps = new CapabilityTimestamps(
            CreatedAt: now,
            UpdatedAt: now,
            InstalledAt: request.InitialState is CapabilityState.Installed or CapabilityState.Ready ? now : null,
            EnabledAt: request.InitialStatus == CapabilityStatus.Enabled ? now : null,
            DisabledAt: request.InitialStatus == CapabilityStatus.Disabled ? now : null);

        var capability = new CapabilityRecord(
            Id: request.Id.Trim(),
            Name: request.Name.Trim(),
            Version: request.Version.Trim(),
            Source: request.Source,
            Runtime: request.Runtime,
            InstallPath: request.InstallPath.Trim(),
            Status: request.InitialStatus,
            State: request.InitialState,
            Permissions: request.Permissions,
            Health: health,
            Timestamps: timestamps,
            Description: request.Description);

        await _store.UpsertCapabilityAsync(capability, cancellationToken).ConfigureAwait(false);
        return capability;
    }

    public Task<CapabilityRecord?> GetAsync(string capabilityId, CancellationToken cancellationToken = default) =>
        _store.GetCapabilityAsync(capabilityId, cancellationToken);

    public async Task<IReadOnlyList<CapabilityRecord>> ListAsync(CapabilityListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new CapabilityListOptions();
        var capabilities = await _store.ListCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

        return capabilities
            .Where(c => options.IncludeRemoved || c.Status != CapabilityStatus.Removed)
            .Where(c => options.Kind is null || c.Source.Kind == options.Kind)
            .Where(c => options.Status is null || c.Status == options.Status)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CapabilityRecord> EnableAsync(string capabilityId, CancellationToken cancellationToken = default)
    {
        var capability = await GetRequiredCapabilityAsync(capabilityId, cancellationToken).ConfigureAwait(false);
        EnsureNotRemoved(capability);

        if (capability.Status == CapabilityStatus.Enabled)
        {
            return capability;
        }

        var now = _timeProvider.GetUtcNow();
        var updated = capability with
        {
            Status = CapabilityStatus.Enabled,
            Timestamps = capability.Timestamps with
            {
                UpdatedAt = now,
                EnabledAt = now
            }
        };

        await _store.UpsertCapabilityAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<CapabilityRecord> DisableAsync(string capabilityId, CancellationToken cancellationToken = default)
    {
        var capability = await GetRequiredCapabilityAsync(capabilityId, cancellationToken).ConfigureAwait(false);
        EnsureNotRemoved(capability);

        if (capability.Status == CapabilityStatus.Disabled)
        {
            return capability;
        }

        var now = _timeProvider.GetUtcNow();
        var updated = capability with
        {
            Status = CapabilityStatus.Disabled,
            Timestamps = capability.Timestamps with
            {
                UpdatedAt = now,
                DisabledAt = now
            }
        };

        await _store.UpsertCapabilityAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<CapabilityRecord> RemoveAsync(string capabilityId, CancellationToken cancellationToken = default)
    {
        var capability = await GetRequiredCapabilityAsync(capabilityId, cancellationToken).ConfigureAwait(false);

        if (capability.Status == CapabilityStatus.Removed)
        {
            return capability;
        }

        var now = _timeProvider.GetUtcNow();
        var updated = capability with
        {
            Status = CapabilityStatus.Removed,
            State = CapabilityState.Removed,
            Timestamps = capability.Timestamps with
            {
                UpdatedAt = now,
                RemovedAt = now
            }
        };

        await _store.UpsertCapabilityAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task<CapabilityRecord> GetRequiredCapabilityAsync(string capabilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("Capability id is required.", nameof(capabilityId));
        }

        var capability = await _store.GetCapabilityAsync(capabilityId.Trim(), cancellationToken).ConfigureAwait(false);
        return capability ?? throw new CapabilityGovernanceException($"Capability '{capabilityId}' was not found.");
    }

    private static void ValidateRegistrationRequest(CapabilityRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            throw new ArgumentException("Capability id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Capability name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new ArgumentException("Capability version is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.InstallPath))
        {
            throw new ArgumentException("Capability install path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Runtime.EntryPoint))
        {
            throw new ArgumentException("Capability runtime entry point is required.", nameof(request));
        }
    }

    private static void EnsureNotRemoved(CapabilityRecord capability)
    {
        if (capability.Status == CapabilityStatus.Removed || capability.State == CapabilityState.Removed)
        {
            throw new CapabilityGovernanceException($"Capability '{capability.Id}' has been removed.");
        }
    }
}
