using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class CvIngestTool(ICvIngestionService cvIngestionService) : IToolHandler
{
    public string Name => "cv_ingest";

    public string Description => "Process and store a CV from the current message's PDF attachment into memory";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "revision_summary": {
                    "type": "string",
                    "description": "A brief description of this CV version or what changed"
                }
            },
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
            if (context.OriginalMessage is null)
            {
                return ToolResult.Error("No message context available. A message with a PDF attachment is required.");
            }

            var pdfAttachment = context.OriginalMessage.Attachments
                .FirstOrDefault(a =>
                    string.Equals(a.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                    || (a.FileName is not null && a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)));

            if (pdfAttachment is null)
            {
                return ToolResult.Error("No PDF attachment found in the current message. Please send a PDF file.");
            }

            if (pdfAttachment.Content is null || pdfAttachment.Content.Length == 0)
            {
                return ToolResult.Error("The PDF attachment has no content. The file may not have been downloaded.");
            }

            string? revisionSummary = null;
            if (arguments.TryGetProperty("revision_summary", out var summaryElement))
            {
                revisionSummary = summaryElement.GetString();
            }

            using var contentStream = new MemoryStream(pdfAttachment.Content);
            var request = new CvUploadIngestionRequest(
                UserProfileId: context.UserId,
                FileName: pdfAttachment.FileName ?? "cv.pdf",
                ContentType: pdfAttachment.ContentType ?? "application/pdf",
                Content: contentStream,
                RevisionSummary: revisionSummary);

            var result = await cvIngestionService.IngestAsync(request, cancellationToken);

            return ToolResult.Success(
                $"CV processed successfully. Revision #{result.CvRevision.RevisionNumber} " +
                $"created (ID: {result.CvRevision.Id}). Summary: \"{result.CvRevision.Summary}\"");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to ingest CV: {ex.Message}");
        }
    }
}
