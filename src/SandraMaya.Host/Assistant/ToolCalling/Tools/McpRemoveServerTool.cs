using System.Text.Json;
using SandraMaya.Host.Mcp;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class McpRemoveServerTool(McpClientManager mcpManager) : IToolHandler
{
    public string Name => "mcp_remove_server";

    public string Description => "Remove a configured MCP server";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "server_id": {
                    "type": "string",
                    "description": "The ID of the MCP server to remove"
                }
            },
            "required": ["server_id"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serverId = arguments.GetProperty("server_id").GetString()
                           ?? throw new ArgumentException("server_id is required");

            var removed = await mcpManager.RemoveServerAsync(serverId, cancellationToken);

            if (!removed)
            {
                return ToolResult.Error($"MCP server '{serverId}' not found.");
            }

            return ToolResult.Success($"MCP server '{serverId}' has been removed.");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to remove MCP server: {ex.Message}");
        }
    }
}
