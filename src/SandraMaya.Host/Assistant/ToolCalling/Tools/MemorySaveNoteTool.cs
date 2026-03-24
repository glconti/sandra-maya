using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Domain;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class MemorySaveNoteTool(IMemoryCommandService memoryCommandService) : IToolHandler
{
    public string Name => "memory_save_note";

    public string Description => "Save a note or piece of information to long-term memory for future reference";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "title": {
                    "type": "string",
                    "description": "A short title for the note"
                },
                "content": {
                    "type": "string",
                    "description": "The markdown content of the note"
                },
                "kind": {
                    "type": "string",
                    "enum": ["UserNotes", "ExtractedMarkdown"],
                    "description": "The kind of document. Defaults to UserNotes"
                }
            },
            "required": ["title", "content"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var title = arguments.GetProperty("title").GetString()
                ?? throw new ArgumentException("title is required");
            var content = arguments.GetProperty("content").GetString()
                ?? throw new ArgumentException("content is required");

            var kind = DocumentKind.UserNotes;
            if (arguments.TryGetProperty("kind", out var kindElement))
            {
                var kindStr = kindElement.GetString();
                if (!string.IsNullOrEmpty(kindStr) && Enum.TryParse<DocumentKind>(kindStr, ignoreCase: true, out var parsed))
                {
                    kind = parsed;
                }
            }

            var document = new MarkdownDocument
            {
                UserProfileId = context.UserId,
                Kind = kind,
                Title = title,
                MarkdownContent = content,
                PlainTextContent = content
            };

            var saved = await memoryCommandService.SaveMarkdownDocumentAsync(document, cancellationToken);

            return ToolResult.Success($"Note saved with ID {saved.Id}. Title: \"{saved.Title}\"");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to save note: {ex.Message}");
        }
    }
}
