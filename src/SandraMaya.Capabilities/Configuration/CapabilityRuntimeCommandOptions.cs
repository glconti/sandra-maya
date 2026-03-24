namespace SandraMaya.Capabilities.Configuration;

public sealed record CapabilityRuntimeCommandOptions
{
    public string DotNetCommand { get; init; } = "dotnet";

    public string NodeCommand { get; init; } = "node";

    public string PlaywrightCommand { get; init; } = "node";

    public string PythonCommand { get; init; } = "python";

    public string PowerShellCommand { get; init; } = "pwsh";

    public string BashCommand { get; init; } = "bash";
}
