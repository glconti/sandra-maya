using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Infrastructure.Services;

public sealed class PlaceholderCoverLetterDraftService(IMemoryQueryService memoryQueryService) : ICoverLetterDraftService
{
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

        var canonicalCv = await memoryQueryService.GetCanonicalCvRevisionAsync(request.UserProfileId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"A canonical CV is required before generating a cover letter for user '{request.UserProfileId}'.");

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
