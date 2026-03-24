using System.Text.Json;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class CapabilityListTool(ICapabilityRegistryService registry) : IToolHandler
{
    public string Name => "capability_list";

    public string Description => "List all installed capabilities/skills and their current status";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "kind": {
                    "type": "string",
                    "enum": ["BuiltIn", "Generated"],
                    "description": "Filter by capability kind"
                },
                "status": {
                    "type": "string",
                    "enum": ["Enabled", "Disabled"],
                    "description": "Filter by capability status"
                }
            }
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CapabilityKind? kind = null;
            CapabilityStatus? status = null;

            if (arguments.TryGetProperty("kind", out var kindElement))
            {
                var kindStr = kindElement.GetString();
                if (!string.IsNullOrEmpty(kindStr) && Enum.TryParse<CapabilityKind>(kindStr, ignoreCase: true, out var parsedKind))
                {
                    kind = parsedKind;
                }
            }

            if (arguments.TryGetProperty("status", out var statusElement))
            {
                var statusStr = statusElement.GetString();
                if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<CapabilityStatus>(statusStr, ignoreCase: true, out var parsedStatus))
                {
                    status = parsedStatus;
                }
            }

            var options = new CapabilityListOptions(Kind: kind, Status: status);
            var capabilities = await registry.ListAsync(options, cancellationToken);

            if (capabilities.Count == 0)
            {
                return ToolResult.Success("No capabilities found matching the specified filters.");
            }

            var projected = capabilities.Select(c => new
            {
                c.Id,
                c.Name,
                c.Version,
                Kind = c.Source.Kind.ToString(),
                Status = c.Status.ToString(),
                c.Description
            });

            return ToolResult.Json(projected);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to list capabilities: {ex.Message}");
        }
    }
}
