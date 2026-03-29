using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Assistant;

public sealed record CopilotSessionSettings(
    string Model,
    ProviderConfig? Provider,
    string WorkingDirectory,
    string ClientName,
    bool UsesByokProvider);

public sealed class CopilotRuntimeConfiguration
{
    private readonly AzureOpenAiOptions _azureOptions;
    private readonly CopilotRuntimeOptions _copilotOptions;
    private readonly IHostEnvironment _environment;

    public CopilotRuntimeConfiguration(
        IOptions<AzureOpenAiOptions> azureOptions,
        IOptions<CopilotRuntimeOptions> copilotOptions,
        IHostEnvironment environment)
    {
        _azureOptions = azureOptions.Value;
        _copilotOptions = copilotOptions.Value;
        _environment = environment;
    }

    public bool TryResolve(out CopilotSessionSettings settings, out string errorMessage)
    {
        if (_azureOptions.IsConfigured)
        {
            settings = new CopilotSessionSettings(
                _azureOptions.DeploymentName!,
                BuildProviderConfig(_azureOptions),
                ResolveWorkingDirectory(),
                ResolveClientName(),
                UsesByokProvider: true);

            errorMessage = string.Empty;
            return true;
        }

        if (_copilotOptions.HasExplicitModel)
        {
            settings = new CopilotSessionSettings(
                _copilotOptions.Model!,
                Provider: null,
                ResolveWorkingDirectory(),
                ResolveClientName(),
                UsesByokProvider: false);

            errorMessage = string.Empty;
            return true;
        }

        settings = default!;
        errorMessage =
            "Copilot runtime is not configured. Set AzureOpenAi:BaseUrl, AzureOpenAi:ApiKey, and AzureOpenAi:DeploymentName for BYOK, or set CopilotRuntime:Model for GitHub-authenticated sessions.";
        return false;
    }

    private string ResolveWorkingDirectory() =>
        string.IsNullOrWhiteSpace(_copilotOptions.WorkingDirectory)
            ? _environment.ContentRootPath
            : _copilotOptions.WorkingDirectory!;

    private string ResolveClientName() =>
        string.IsNullOrWhiteSpace(_copilotOptions.ClientName)
            ? "Maya"
            : _copilotOptions.ClientName;

    private static ProviderConfig BuildProviderConfig(AzureOpenAiOptions options)
    {
        var provider = new ProviderConfig
        {
            Type = options.ProviderType,
            BaseUrl = options.BaseUrl!,
            ApiKey = options.ApiKey!
        };

        if (!string.IsNullOrWhiteSpace(options.WireApi))
        {
            provider.WireApi = options.WireApi;
        }

        if (string.Equals(options.ProviderType, "azure", StringComparison.OrdinalIgnoreCase))
        {
            provider.Azure = new AzureOptions
            {
                ApiVersion = options.ApiVersion
            };
        }

        return provider;
    }
}
