using System.Text.Json;
using SandraMaya.Application.Abstractions;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class MemorySearchTool(IMemoryQueryService memoryQueryService) : IToolHandler
{
    public string Name => "memory_search";

    public string Description => "Search saved notes and documents in long-term memory by keyword";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query to find relevant documents"
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of results to return. Defaults to 10"
                }
            },
            "required": ["query"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = arguments.GetProperty("query").GetString()
                ?? throw new ArgumentException("query is required");

            var limit = 10;
            if (arguments.TryGetProperty("limit", out var limitElement))
            {
                limit = limitElement.GetInt32();
            }

            var results = await memoryQueryService.SearchDocumentsAsync(
                context.UserId, query, limit, cancellationToken);

            if (results.Count == 0)
            {
                return ToolResult.Success("No documents found matching the query.");
            }

            var projected = results.Select(r => new
            {
                r.DocumentId,
                r.Title,
                Kind = r.Kind.ToString(),
                r.Snippet,
                r.Rank
            });

            return ToolResult.Json(projected);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to search memory: {ex.Message}");
        }
    }
}
