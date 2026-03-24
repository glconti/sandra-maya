using System.Text.Json;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Infrastructure.Helpers;

namespace SandraMaya.Infrastructure.Jobs;

public sealed class JobPostingImportMapper
{
    public JobPosting MapPosting(
        Guid userProfileId,
        string sourceSite,
        DiscoveredJobPosting job,
        Guid? descriptionDocumentId,
        DateTimeOffset lastSeenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSite);
        ArgumentNullException.ThrowIfNull(job);

        var posting = new JobPosting
        {
            UserProfileId = userProfileId,
            SourceSite = sourceSite.Trim(),
            SourcePostingId = NormalizeOptional(job.SourcePostingId),
            SourceUrl = job.SourceUrl.Trim(),
            Title = job.Title.Trim(),
            CompanyName = job.CompanyName.Trim(),
            Location = job.Location.Trim(),
            EmploymentType = job.EmploymentType.Trim(),
            CompensationText = job.CompensationText.Trim(),
            DescriptionDocumentId = descriptionDocumentId,
            RawPayloadJson = string.IsNullOrWhiteSpace(job.RawPayloadJson) ? "{}" : job.RawPayloadJson,
            IsActive = job.IsActive,
            LastSeenAtUtc = lastSeenAtUtc,
            PostedAtUtc = job.PostedAtUtc
        };

        posting.DedupeKey = string.IsNullOrWhiteSpace(job.DedupeKey)
            ? MemoryTextNormalizer.BuildJobPostingDedupeKey(posting)
            : job.DedupeKey.Trim();

        return posting;
    }

    public MarkdownDocument? MapDescriptionDocument(
        Guid userProfileId,
        string sourceSite,
        DiscoveredJobPosting job,
        Guid? documentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSite);
        ArgumentNullException.ThrowIfNull(job);

        var markdown = string.IsNullOrWhiteSpace(job.DescriptionMarkdown)
            ? job.DescriptionPlainText?.Trim()
            : job.DescriptionMarkdown.Trim();

        if (string.IsNullOrWhiteSpace(markdown) && string.IsNullOrWhiteSpace(job.DescriptionPlainText))
        {
            return null;
        }

        return new MarkdownDocument
        {
            Id = documentId ?? Guid.NewGuid(),
            UserProfileId = userProfileId,
            Kind = DocumentKind.JobPosting,
            Title = $"{job.Title.Trim()} ({job.CompanyName.Trim()})",
            MarkdownContent = markdown ?? string.Empty,
            PlainTextContent = job.DescriptionPlainText ?? string.Empty,
            MetadataJson = JsonSerializer.Serialize(new
            {
                sourceSite,
                sourcePostingId = NormalizeOptional(job.SourcePostingId),
                sourceUrl = job.SourceUrl.Trim()
            })
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
