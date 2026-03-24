using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Services;

/// <summary>
/// Generates cover letter drafts using Azure OpenAI chat completions.
/// Falls back to placeholder output when Azure OpenAI is not configured.
/// </summary>
public sealed class AzureOpenAiCoverLetterDraftService(
    IMemoryQueryService memoryQueryService,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AzureOpenAiCoverLetterDraftService> logger) : ICoverLetterDraftService
{
    private readonly AzureOpenAiOptions _options = options.Value;

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

        if (!_options.IsConfigured)
        {
            logger.LogWarning("Azure OpenAI is not configured — returning placeholder cover letter.");
            return BuildPlaceholder(request, user, jobPosting, canonicalCv);
        }

        var draftMarkdown = await GenerateWithAzureOpenAiAsync(
            request, user, jobPosting, cvText, cancellationToken);

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

    private async Task<string> GenerateWithAzureOpenAiAsync(
        CoverLetterDraftRequest request,
        UserProfile user,
        JobPosting jobPosting,
        string cvText,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(request, user, jobPosting, cvText);

        var client = new AzureOpenAIClient(
            new Uri(_options.BaseUrl!),
            new AzureKeyCredential(_options.ApiKey!));

        var chatClient = client.GetChatClient(_options.DeploymentName!);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a professional cover letter writer. " +
                "Write concise, compelling cover letters tailored to the job posting and the applicant's experience. " +
                "Output only the cover letter text in Markdown format, with no preamble or commentary."),
            new UserChatMessage(prompt)
        };

        logger.LogInformation(
            "Requesting cover letter from Azure OpenAI for job '{JobTitle}' at '{Company}'.",
            jobPosting.Title,
            jobPosting.CompanyName);

        var completion = (await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken)).Value;

        var content = completion.Content.Count > 0 && completion.Content[0].Text is not null
            ? completion.Content[0].Text
            : string.Empty;

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

        // Prefer the document linked to the canonical CV revision
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
