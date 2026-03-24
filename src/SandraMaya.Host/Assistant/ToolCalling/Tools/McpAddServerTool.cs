using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SandraMaya.Host.Mcp;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class McpAddServerTool(McpClientManager mcpManager) : IToolHandler
{
    public string Name => "mcp_add_server";

    public string Description =>
        "Add a new MCP server configuration. The server will be available after restart or when MCP connections are implemented.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "Human-readable name for the MCP server"
                },
                "command": {
                    "type": "string",
                    "description": "The command to start the MCP server process"
                },
                "arguments": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Command-line arguments for the server"
                },
                "environment": {
                    "type": "object",
                    "additionalProperties": { "type": "string" },
                    "description": "Environment variables to set for the server process"
                }
            },
            "required": ["name", "command"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var name = arguments.GetProperty("name").GetString()
                       ?? throw new ArgumentException("name is required");
            var command = arguments.GetProperty("command").GetString()
                          ?? throw new ArgumentException("command is required");

            string[]? args = null;
            if (arguments.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array)
            {
                args = argsElement.EnumerateArray()
                    .Select(a => a.GetString() ?? string.Empty)
                    .ToArray();
            }

            Dictionary<string, string>? env = null;
            if (arguments.TryGetProperty("environment", out var envElement) && envElement.ValueKind == JsonValueKind.Object)
            {
                env = new Dictionary<string, string>();
                foreach (var prop in envElement.EnumerateObject())
                {
                    env[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            var serverId = GenerateServerId(name);

            var config = new McpServerConfiguration
            {
                Id = serverId,
                Name = name,
                Transport = "stdio",
                Command = command,
                Arguments = args,
                EnvironmentVariables = env,
                Enabled = true
            };

            await mcpManager.AddServerAsync(config, cancellationToken);

            return ToolResult.Json(new
            {
                serverId,
                name,
                command,
                message = $"MCP server '{name}' configured successfully. " +
                          "Note: actual MCP connections are not yet implemented — the server is registered for future use."
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to add MCP server: {ex.Message}");
        }
    }

    private static string GenerateServerId(string name)
    {
        var slug = string.Concat(name.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '))
            .Replace(' ', '-')
            .Trim('-');

        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{slug}-{DateTimeOffset.UtcNow.Ticks}")));

        return $"{slug}-{hash[..6].ToLowerInvariant()}";
    }
}
