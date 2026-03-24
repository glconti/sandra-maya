using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Health;

public sealed class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IOptions<TelegramOptions> _telegramOptions;
    private readonly IOptions<AzureOpenAiOptions> _azureOptions;
    private readonly StorageLayout _storageLayout;

    public ConfigurationHealthCheck(
        IOptions<TelegramOptions> telegramOptions,
        IOptions<AzureOpenAiOptions> azureOptions,
        StorageLayout storageLayout)
    {
        _telegramOptions = telegramOptions;
        _azureOptions = azureOptions;
        _storageLayout = storageLayout;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["telegramConfigured"] = !string.IsNullOrWhiteSpace(_telegramOptions.Value.BotToken),
            ["azureConfigured"] = _azureOptions.Value.IsConfigured,
            ["azureProviderType"] = _azureOptions.Value.ProviderType,
            ["azureDeploymentName"] = _azureOptions.Value.DeploymentName ?? string.Empty,
            ["storageRoot"] = _storageLayout.Root,
            ["sqlitePath"] = _storageLayout.SqlitePath,
            ["uploadsPath"] = _storageLayout.UploadsPath,
            ["capabilitiesPath"] = _storageLayout.CapabilitiesPath,
            ["generatedCapabilitiesPath"] = _storageLayout.GeneratedCapabilitiesPath
        };

        if (string.IsNullOrWhiteSpace(_telegramOptions.Value.BotToken))
        {
            return Task.FromResult(HealthCheckResult.Degraded("Telegram bot token is missing; polling is disabled.", data: data));
        }

        var missingPaths = new[]
        {
            _storageLayout.Root,
            _storageLayout.UploadsPath,
            _storageLayout.CapabilitiesPath,
            _storageLayout.GeneratedCapabilitiesPath,
            _storageLayout.WorkPath,
            _storageLayout.TempPath,
            Path.GetDirectoryName(_storageLayout.SqlitePath) ?? _storageLayout.Root
        }
        .Where(path => !Directory.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        if (missingPaths.Length > 0)
        {
            data["missingStoragePaths"] = missingPaths;
            return Task.FromResult(HealthCheckResult.Unhealthy("One or more storage paths are missing.", data: data));
        }

        if (!_azureOptions.Value.IsConfigured)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Azure OpenAI settings are not fully configured.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Configuration is loaded.", data));
    }
}
