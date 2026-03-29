using System.ComponentModel.DataAnnotations;

namespace SandraMaya.Host.Configuration;

public sealed class CopilotRuntimeOptions
{
    public const string SectionName = "CopilotRuntime";

    public string? CliPath { get; init; }

    [RegularExpression("^(none|error|warning|info|debug|all)$", ErrorMessage = "CopilotRuntime:LogLevel must be one of none, error, warning, info, debug, or all.")]
    public string LogLevel { get; init; } = "info";

    public string? GitHubToken { get; init; }

    public bool UseLoggedInUser { get; init; } = true;

    public string? Model { get; init; }

    public bool UseStdio { get; init; } = true;

    public bool AutoStart { get; init; } = true;

    public string? WorkingDirectory { get; init; }

    public string ClientName { get; init; } = "Maya";

    public bool HasExplicitModel =>
        !string.IsNullOrWhiteSpace(Model);

    public bool HasAuthenticationPath =>
        !string.IsNullOrWhiteSpace(GitHubToken) || UseLoggedInUser;
}
