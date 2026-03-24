using System.Text.Json;
using SandraMaya.Capabilities.Abstractions;
using SandraMaya.Capabilities.Models;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

/// <summary>
/// Lets the assistant enable or disable one or more capabilities directly from chat.
/// This allows the user to say things like "enable the Indeed crawler" and have
/// the assistant call this tool to make the change without any manual file editing.
/// </summary>
public sealed class CapabilitySetStatusTool(ICapabilityRegistryService registry) : IToolHandler
{
    public string Name => "capability_set_status";

    public string Description =>
        "Enable or disable one or more installed capabilities. " +
        "Use capability_list to discover capability IDs first. " +
        "Supports enabling or disabling multiple capabilities at once.";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["enable", "disable"],
                    "description": "Whether to enable or disable the capabilities"
                },
                "capability_ids": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "One or more capability IDs to change status for"
                }
            },
            "required": ["action", "capability_ids"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actionStr = arguments.GetProperty("action").GetString()
                ?? throw new ArgumentException("action is required");

            if (!string.Equals(actionStr, "enable", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actionStr, "disable", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResult.Error("action must be 'enable' or 'disable'.");
            }

            var enabling = string.Equals(actionStr, "enable", StringComparison.OrdinalIgnoreCase);

            if (!arguments.TryGetProperty("capability_ids", out var idsElement) ||
                idsElement.ValueKind != JsonValueKind.Array)
            {
                return ToolResult.Error("capability_ids must be a non-empty array of strings.");
            }

            var ids = idsElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            if (ids.Count == 0)
                return ToolResult.Error("capability_ids must contain at least one ID.");

            var results = new List<object>();

            foreach (var id in ids)
            {
                try
                {
                    var updated = enabling
                        ? await registry.EnableAsync(id, cancellationToken)
                        : await registry.DisableAsync(id, cancellationToken);

                    results.Add(new
                    {
                        id = updated.Id,
                        name = updated.Name,
                        status = updated.Status.ToString(),
                        ok = true
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { id, ok = false, error = ex.Message });
                }
            }

            var allOk = results.All(r =>
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(r));
                return doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean();
            });

            return allOk
                ? ToolResult.Json(results)
                : ToolResult.Error(JsonSerializer.Serialize(results));
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to set capability status: {ex.Message}");
        }
    }
}
