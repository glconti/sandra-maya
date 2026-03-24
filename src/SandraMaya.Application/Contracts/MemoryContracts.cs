using SandraMaya.Application.Domain;

namespace SandraMaya.Application.Contracts;

public sealed record FileStorageWriteRequest(
    string FileName,
    string ContentType,
    StoragePartition Partition,
    Stream Content);

public sealed record StoredFileDescriptor(
    string RelativePath,
    string AbsolutePath,
    string ContentType,
    long ByteCount,
    string Sha256);

public sealed record PdfToMarkdownResult(
    bool Succeeded,
    string Markdown,
    string PlainText,
    string? FailureReason = null);

public sealed record MarkdownDocumentSearchResult(
    Guid DocumentId,
    string Title,
    DocumentKind Kind,
    string Snippet,
    double Rank);

public sealed record JobPostingQuery(
    string? SearchText = null,
    string? SourceSite = null,
    string? Location = null,
    bool ActiveOnly = true,
    int Limit = 20);

public sealed record JobApplicationStatusUpdateRequest(
    Guid UserProfileId,
    Guid JobPostingId,
    JobApplicationStatus Status,
    Guid? CvRevisionId = null,
    string NotesMarkdown = "",
    string MetadataJson = "{}",
    DateTimeOffset? AppliedAtUtc = null);

public sealed record JobApplicationListQuery(
    IReadOnlyCollection<JobApplicationStatus>? Statuses = null,
    int Limit = 50);

public sealed record JobApplicationState(
    JobPosting JobPosting,
    JobApplicationStatusRecord? StatusRecord)
{
    public JobApplicationStatus? CurrentStatus => StatusRecord?.Status;
    public bool IsTracked => StatusRecord is not null;
}

public enum JobActivitySummaryPeriod
{
    Weekly = 1,
    Monthly = 2
}

public sealed record JobStatusCount(
    JobApplicationStatus Status,
    int Count);

public sealed record JobDiscoveryActivity(
    Guid JobPostingId,
    string Title,
    string CompanyName,
    string SourceUrl,
    DateTimeOffset FirstSeenAtUtc);

public sealed record JobApplicationActivity(
    Guid JobPostingId,
    string Title,
    string CompanyName,
    JobApplicationStatus Status,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? AppliedAtUtc);

public sealed record JobActivitySummary(
    JobActivitySummaryPeriod Period,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int JobsDiscovered,
    int JobsTracked,
    int ApplicationsSubmitted,
    int InterviewsAdvanced,
    int OffersReceived,
    int RejectionsLogged,
    int WithdrawalsLogged,
    IReadOnlyList<JobStatusCount> CurrentStatusCounts,
    IReadOnlyList<JobDiscoveryActivity> RecentDiscoveries,
    IReadOnlyList<JobApplicationActivity> RecentApplicationUpdates);

public sealed record CoverLetterDraftRequest(
    Guid UserProfileId,
    Guid JobPostingId,
    string? AdditionalGuidance = null,
    string Tone = "professional",
    string Language = "en");

public sealed record CoverLetterDraftResult(
    Guid UserProfileId,
    Guid JobPostingId,
    Guid CvRevisionId,
    string JobTitle,
    string CompanyName,
    string DraftMarkdown,
    bool IsPlaceholder,
    string PromptHint,
    DateTimeOffset GeneratedAtUtc);

public sealed record CvUploadIngestionRequest(
    Guid UserProfileId,
    string FileName,
    string ContentType,
    Stream Content,
    string? RevisionSummary = null,
    string? DocumentTitle = null);

public sealed record CvUploadIngestionResult(
    StoredAsset RawAsset,
    StoredAsset MarkdownArtifact,
    MarkdownDocument MarkdownDocument,
    CvRevision CvRevision);
