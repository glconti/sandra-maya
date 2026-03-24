using System.Text.Json;
using System.Text.Json.Serialization;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Capabilities.Persistence;

public sealed class FileCapabilityStore(CapabilityStoreOptions options) : ICapabilityStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CapabilityStoreOptions _options = options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<CapabilityRecord?> GetCapabilityAsync(string capabilityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Capabilities.FirstOrDefault(c => string.Equals(c.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<CapabilityRecord>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Capabilities
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpsertCapabilityAsync(CapabilityRecord capability, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Capabilities.RemoveAll(c => string.Equals(c.Id, capability.Id, StringComparison.OrdinalIgnoreCase));
            document.Capabilities.Add(capability);
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AddInstallRecordAsync(CapabilityInstallProvenanceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Installations.Add(record);
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<CapabilityInstallProvenanceRecord>> ListInstallRecordsAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Installations
                .Where(r => string.Equals(r.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.InstalledAt)
                .ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AddExecutionRecordAsync(CapabilityExecutionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Executions.Add(record);
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<CapabilityExecutionRecord>> ListExecutionRecordsAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Executions
                .Where(r => string.Equals(r.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.RequestedAt)
                .ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<CapabilityRegistryDocument> LoadAsync(CancellationToken cancellationToken)
    {
        EnsureStoreDirectory();

        if (!File.Exists(_options.RegistryFilePath))
        {
            return new CapabilityRegistryDocument();
        }

        await using var stream = File.OpenRead(_options.RegistryFilePath);
        var document = await JsonSerializer.DeserializeAsync<CapabilityRegistryDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return document ?? new CapabilityRegistryDocument();
    }

    private async Task SaveAsync(CapabilityRegistryDocument document, CancellationToken cancellationToken)
    {
        EnsureStoreDirectory();

        await using var stream = File.Create(_options.RegistryFilePath);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureStoreDirectory()
    {
        Directory.CreateDirectory(_options.RootPath);
    }
}
