namespace SandraMaya.Host.Configuration;

public sealed class RuntimeOptions
{
    public const string SectionName = "Runtime";

    public string PlaywrightCommand { get; init; } = "node";
}
