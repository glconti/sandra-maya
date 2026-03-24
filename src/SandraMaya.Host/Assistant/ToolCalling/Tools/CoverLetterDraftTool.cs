using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

/// <summary>
/// Tool handler that generates a personalized cover letter for a specific job posting.
/// </summary>
public sealed class CoverLetterDraftTool(ICoverLetterDraftService coverLetterDraftService) : IToolHandler
{
    public string Name => "cover_letter_draft";

    public string Description =>
        "Generate a personalized cover letter for a specific job posting using the user's CV";

    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "job_posting_id": {
                    "type": "string",
                    "description": "The GUID of the job posting to generate a cover letter for"
                },
                "tone": {
                    "type": "string",
                    "description": "The desired tone of the cover letter (e.g. professional, friendly, enthusiastic)",
                    "default": "professional"
                },
                "language": {
                    "type": "string",
                    "description": "The language to write the cover letter in (ISO 639-1 code)",
                    "default": "en"
                },
                "additional_guidance": {
                    "type": "string",
                    "description": "Optional extra instructions for tailoring the cover letter"
                }
            },
            "required": ["job_posting_id"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!arguments.TryGetProperty("job_posting_id", out var jobPostingIdElement))
            {
                return ToolResult.Error("Missing required parameter: job_posting_id");
            }

            if (!Guid.TryParse(jobPostingIdElement.GetString(), out var jobPostingId))
            {
                return ToolResult.Error("Invalid job_posting_id — expected a valid GUID.");
            }

            var tone = arguments.TryGetProperty("tone", out var toneElement)
                ? toneElement.GetString() ?? "professional"
                : "professional";

            var language = arguments.TryGetProperty("language", out var langElement)
                ? langElement.GetString() ?? "en"
                : "en";

            string? additionalGuidance = arguments.TryGetProperty("additional_guidance", out var guidanceElement)
                ? guidanceElement.GetString()
                : null;

            var request = new CoverLetterDraftRequest(
                context.UserId,
                jobPostingId,
                additionalGuidance,
                tone,
                language);

            var result = await coverLetterDraftService.GenerateDraftAsync(request, cancellationToken);

            return ToolResult.Json(new
            {
                result.JobTitle,
                result.CompanyName,
                result.DraftMarkdown,
                result.IsPlaceholder,
                GeneratedAtUtc = result.GeneratedAtUtc.ToString("O")
            });
        }
        catch (InvalidOperationException ex)
        {
            return ToolResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to generate cover letter: {ex.Message}");
        }
    }
}
