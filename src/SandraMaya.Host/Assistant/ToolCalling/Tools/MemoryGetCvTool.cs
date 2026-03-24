using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Domain;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class MemoryGetCvTool(IMemoryQueryService memoryQueryService) : IToolHandler
{
    public string Name => "memory_get_cv";

    public string Description => "Retrieve the user's current canonical CV text from memory";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revision = await memoryQueryService.GetCanonicalCvRevisionAsync(
                context.UserId, cancellationToken);

            if (revision is null)
            {
                return ToolResult.Success("No CV has been uploaded yet.");
            }

            // Search for the CV document content associated with this revision
            var documents = await memoryQueryService.SearchDocumentsAsync(
                context.UserId, "cv", limit: 20, cancellationToken);

            var cvDoc = documents
                .Where(d => d.Kind == DocumentKind.Cv)
                .OrderByDescending(d => d.Rank)
                .FirstOrDefault(d => d.DocumentId == revision.MarkdownDocumentId)
                ?? documents
                    .Where(d => d.Kind == DocumentKind.Cv)
                    .OrderByDescending(d => d.Rank)
                    .FirstOrDefault();

            var result = new
            {
                revision.RevisionNumber,
                revision.IsCanonical,
                revision.Summary,
                UploadedAtUtc = revision.UploadedAtUtc.ToString("O"),
                revision.MarkdownDocumentId,
                DocumentTitle = cvDoc?.Title,
                DocumentSnippet = cvDoc?.Snippet
            };

            return ToolResult.Json(result);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to retrieve CV: {ex.Message}");
        }
    }
}
