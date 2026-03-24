namespace SandraMaya.Host.Mcp;

public sealed class McpServerConfiguration
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Transport { get; init; } = "stdio";
    public string Command { get; init; } = string.Empty;
    public string[]? Arguments { get; init; }
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
    public string? Url { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed class McpServersConfig
{
    public List<McpServerConfiguration> Servers { get; init; } = new();
}
