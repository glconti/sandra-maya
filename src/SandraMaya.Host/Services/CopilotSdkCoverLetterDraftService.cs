using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Services;

/// <summary>
/// Generates cover letter drafts through the GitHub Copilot SDK runtime.
/// Falls back to placeholder output when no Copilot runtime configuration is available.
/// </summary>
public sealed class CopilotSdkCoverLetterDraftService(
    IMemoryQueryService memoryQueryService,
    ICopilotClientProvider clientProvider,
    CopilotRuntimeConfiguration runtimeConfiguration,
    IOptions<CopilotRuntimeOptions> copilotOptions,
    ILogger<CopilotSdkCoverLetterDraftService> logger) : ICoverLetterDraftService
{
    private readonly CopilotRuntimeOptions _copilotOptions = copilotOptions.Value;

    public async Task<CoverLetterDraftResult> GenerateDraftAsync(
        CoverLetterDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await memoryQueryService.GetUserAsync(request.UserProfileId, cancellationToken)
            ?? throw new InvalidOperationException($"User '{request.UserProfileId}' was not found.");

        var jobPosting = await memoryQueryService.GetJobPostingAsync(
            request.UserProfileId,
            request.JobPostingId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Job posting '{request.JobPostingId}' was not found for user '{request.UserProfileId}'.");

        var canonicalCv = await memoryQueryService.GetCanonicalCvRevisionAsync(
            request.UserProfileId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"A canonical CV is required before generating a cover letter for user '{request.UserProfileId}'.");

        var cvText = await LoadCvTextAsync(request.UserProfileId, canonicalCv, cancellationToken);

        if (!runtimeConfiguration.TryResolve(out var settings, out _))
        {
            logger.LogWarning("Copilot runtime is not configured — returning placeholder cover letter.");
            return BuildPlaceholder(request, user, jobPosting, canonicalCv);
        }

        var draftMarkdown = await GenerateWithCopilotSdkAsync(
            settings,
            request,
            user,
            jobPosting,
            cvText,
            cancellationToken);

        return new CoverLetterDraftResult(
            request.UserProfileId,
            request.JobPostingId,
            canonicalCv.Id,
            jobPosting.Title,
            jobPosting.CompanyName,
            draftMarkdown,
            IsPlaceholder: false,
            PromptHint: string.Empty,
            DateTimeOffset.UtcNow);
    }

    private async Task<string> GenerateWithCopilotSdkAsync(
        CopilotSessionSettings settings,
        CoverLetterDraftRequest request,
        UserProfile user,
        JobPosting jobPosting,
        string cvText,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(request, user, jobPosting, cvText);
        var client = await clientProvider.GetClientAsync(cancellationToken);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            ClientName = settings.ClientName,
            Model = settings.Model,
            Provider = settings.Provider,
            WorkingDirectory = settings.WorkingDirectory,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content =
                    """
                    You are a professional cover letter writer.
                    Write concise, compelling cover letters tailored to the job posting and the applicant's experience.
                    Output only the cover letter text in Markdown format, with no preamble or commentary.
                    """
            },
            AvailableTools = [],
            OnPermissionRequest = PermissionHandler.ApproveAll,
            InfiniteSessions = new InfiniteSessionConfig
            {
                Enabled = false
            }
        }, cancellationToken);

        logger.LogInformation(
            "Requesting cover letter from Copilot SDK runtime for job '{JobTitle}' at '{Company}' using {Mode}.",
            jobPosting.Title,
            jobPosting.CompanyName,
            settings.UsesByokProvider ? "BYOK" : "GitHub-authenticated");

        var response = await session.SendAndWaitAsync(
            new MessageOptions
            {
                Prompt = prompt
            },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken);

        var content = response?.Data?.Content ?? string.Empty;

        logger.LogInformation(
            "Received cover letter draft ({Length} chars) for job '{JobTitle}'.",
            content.Length,
            jobPosting.Title);

        return content;
    }

    private static string BuildPrompt(
        CoverLetterDraftRequest request,
        UserProfile user,
        JobPosting jobPosting,
        string cvText)
    {
        var guidanceLine = string.IsNullOrWhiteSpace(request.AdditionalGuidance)
            ? string.Empty
            : $"\nAdditional guidance: {request.AdditionalGuidance.Trim()}";

        return
            $"""
             Write a cover letter for the following job posting.

             Job: {jobPosting.Title} at {jobPosting.CompanyName} in {FormatValue(jobPosting.Location)}
             Job URL: {FormatValue(jobPosting.SourceUrl)}

             Applicant name: {user.DisplayName}

             Applicant's CV:
             {cvText}

             Tone: {request.Tone}
             Language: {request.Language}{guidanceLine}

             Write a professional, personalized cover letter that highlights relevant experience from the CV.
             """;
    }

    private async Task<string> LoadCvTextAsync(
        Guid userId,
        CvRevision canonicalCv,
        CancellationToken cancellationToken)
    {
        var documents = await memoryQueryService.SearchDocumentsAsync(
            userId, "cv", limit: 20, cancellationToken);

        var cvDoc = documents
            .Where(d => d.Kind == DocumentKind.Cv)
            .OrderByDescending(d => d.Rank)
            .FirstOrDefault(d => d.DocumentId == canonicalCv.MarkdownDocumentId)
            ?? documents
                .Where(d => d.Kind == DocumentKind.Cv)
                .OrderByDescending(d => d.Rank)
                .FirstOrDefault();

        if (cvDoc is null)
        {
            logger.LogWarning("No CV document found for user {UserId}.", userId);
            return canonicalCv.Summary ?? "No CV content available.";
        }

        return cvDoc.Snippet;
    }

    private static CoverLetterDraftResult BuildPlaceholder(
        CoverLetterDraftRequest request,
        UserProfile user,
        JobPosting jobPosting,
        CvRevision canonicalCv)
    {
        var guidance = string.IsNullOrWhiteSpace(request.AdditionalGuidance)
            ? "No additional guidance provided."
            : request.AdditionalGuidance.Trim();

        var draftMarkdown =
            $"""
             # Cover Letter Draft — {jobPosting.Title} at {jobPosting.CompanyName}

             Dear Hiring Team,

             I am excited to apply for the {jobPosting.Title} position at {jobPosting.CompanyName}. This placeholder draft is grounded in the canonical CV currently stored for {user.DisplayName} and is intended to be refined by a future model-backed drafting step before anything is shared externally.

             ## Why I am a strong fit
             - [Summarize the most relevant experience from canonical CV revision {canonicalCv.RevisionNumber}.]
             - [Connect that experience to the responsibilities for {jobPosting.Title}.]
             - [Add a company-specific motivation statement for {jobPosting.CompanyName}.]

             ## Context for the next drafting step
             - Job location: {FormatValue(jobPosting.Location)}
             - Employment type: {FormatValue(jobPosting.EmploymentType)}
             - Canonical CV summary: {FormatValue(canonicalCv.Summary)}
             - Preferred tone: {FormatValue(request.Tone)}
             - Preferred language: {FormatValue(request.Language)}
             - Additional guidance: {guidance}
             - Source posting: {jobPosting.SourceUrl}

             Kind regards,

             {user.DisplayName}
             """;

        var promptHint =
            $"Use canonical CV revision '{canonicalCv.Id}' and job posting '{jobPosting.Id}' as the grounding inputs when replacing this placeholder with a model-generated draft.";

        return new CoverLetterDraftResult(
            request.UserProfileId,
            request.JobPostingId,
            canonicalCv.Id,
            jobPosting.Title,
            jobPosting.CompanyName,
            draftMarkdown,
            IsPlaceholder: true,
            promptHint,
            DateTimeOffset.UtcNow);
    }

    private static string FormatValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not specified" : value.Trim();
}
