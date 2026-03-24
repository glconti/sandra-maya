using System.Text.Json;
using SandraMaya.Host.Mcp;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class McpListServersTool(McpClientManager mcpManager) : IToolHandler
{
    public string Name => "mcp_list_servers";

    public string Description => "List configured MCP (Model Context Protocol) servers";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {}
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var servers = await mcpManager.GetServersAsync(cancellationToken);

            if (servers.Count == 0)
            {
                return ToolResult.Success("No MCP servers configured.");
            }

            var projected = servers.Select(s => new
            {
                s.Id,
                s.Name,
                s.Transport,
                s.Command,
                s.Arguments,
                s.Url,
                s.Enabled
            });

            return ToolResult.Json(projected);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to list MCP servers: {ex.Message}");
        }
    }
}
