namespace SandraMaya.Host.Configuration;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAi";

    public string ProviderType { get; init; } = "azure";

    public string? BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? DeploymentName { get; init; }

    public string ApiVersion { get; init; } = "2024-10-21";

    public string? WireApi { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(DeploymentName);
}
