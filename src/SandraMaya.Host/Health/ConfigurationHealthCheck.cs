using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Health;

public sealed class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IOptions<TelegramOptions> _telegramOptions;
    private readonly IOptions<AzureOpenAiOptions> _azureOptions;
    private readonly IOptions<CopilotRuntimeOptions> _copilotOptions;
    private readonly StorageLayout _storageLayout;

    public ConfigurationHealthCheck(
        IOptions<TelegramOptions> telegramOptions,
        IOptions<AzureOpenAiOptions> azureOptions,
        IOptions<CopilotRuntimeOptions> copilotOptions,
        StorageLayout storageLayout)
    {
        _telegramOptions = telegramOptions;
        _azureOptions = azureOptions;
        _copilotOptions = copilotOptions;
        _storageLayout = storageLayout;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["telegramConfigured"] = !string.IsNullOrWhiteSpace(_telegramOptions.Value.BotToken),
            ["copilotCliPath"] = _copilotOptions.Value.CliPath ?? string.Empty,
            ["copilotModel"] = _copilotOptions.Value.Model ?? string.Empty,
            ["copilotUseLoggedInUser"] = _copilotOptions.Value.UseLoggedInUser,
            ["copilotGitHubTokenConfigured"] = !string.IsNullOrWhiteSpace(_copilotOptions.Value.GitHubToken),
            ["copilotByokConfigured"] = _azureOptions.Value.IsConfigured,
            ["copilotByokProviderType"] = _azureOptions.Value.ProviderType,
            ["copilotByokDeploymentName"] = _azureOptions.Value.DeploymentName ?? string.Empty,
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

        if (!_azureOptions.Value.IsConfigured && string.IsNullOrWhiteSpace(_copilotOptions.Value.Model))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Copilot runtime settings are not fully configured. Provide AzureOpenAi settings for BYOK or CopilotRuntime:Model for GitHub-authenticated sessions.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Configuration is loaded.", data));
    }
}
