using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Application.Abstractions;

public interface IFileStorage
{
    Task<StoredFileDescriptor> SaveAsync(FileStorageWriteRequest request, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    string GetAbsolutePath(string relativePath);
}

public interface IPdfToMarkdownConverter
{
    Task<PdfToMarkdownResult> ConvertAsync(Stream pdfContent, string fileName, CancellationToken cancellationToken = default);
}

public interface IMemoryCommandService
{
    Task<UserProfile> SaveUserAsync(UserProfile user, CancellationToken cancellationToken = default);
    Task<AssistantProfileState> SaveAssistantProfileStateAsync(AssistantProfileState state, CancellationToken cancellationToken = default);
    Task<StoredAsset> SaveAssetMetadataAsync(StoredAsset asset, CancellationToken cancellationToken = default);
    Task<MarkdownDocument> SaveMarkdownDocumentAsync(MarkdownDocument document, CancellationToken cancellationToken = default);
    Task<StructuredProfileSnapshot> SaveStructuredProfileSnapshotAsync(StructuredProfileSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<CvRevision> SaveCvRevisionAsync(CvRevision revision, bool makeCanonical = true, CancellationToken cancellationToken = default);
    Task<JobPosting> UpsertJobPostingAsync(JobPosting posting, CancellationToken cancellationToken = default);
    Task<JobApplicationStatusRecord> SaveJobApplicationStatusAsync(JobApplicationStatusRecord record, CancellationToken cancellationToken = default);
}

public interface IMemoryQueryService
{
    Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AssistantProfileState?> GetAssistantProfileStateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredAsset>> GetAssetsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CvRevision>> GetCvRevisionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CvRevision?> GetCanonicalCvRevisionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StructuredProfileSnapshot?> GetLatestStructuredProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchDocumentsAsync(Guid userId, string query, int limit = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> SearchJobPostingsAsync(Guid userId, JobPostingQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> GetJobPostingsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<JobPosting?> GetJobPostingAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default);
    Task<JobPosting?> GetJobPostingByDedupeKeyAsync(Guid userId, string dedupeKey, CancellationToken cancellationToken = default);
    Task<JobApplicationStatusRecord?> GetJobApplicationStatusAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobApplicationStatusRecord>> GetJobApplicationStatusesAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ICvIngestionService
{
    Task<CvUploadIngestionResult> IngestAsync(CvUploadIngestionRequest request, CancellationToken cancellationToken = default);
}

public interface IJobApplicationTrackingService
{
    Task<JobApplicationState> MarkStatusAsync(JobApplicationStatusUpdateRequest request, CancellationToken cancellationToken = default);
    Task<JobApplicationState?> GetCurrentStateAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobApplicationState>> ListApplicationsAsync(Guid userId, JobApplicationListQuery query, CancellationToken cancellationToken = default);
}

public interface IJobActivityReportingService
{
    Task<JobActivitySummary> GetWeeklySummaryAsync(Guid userId, DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default);
    Task<JobActivitySummary> GetMonthlySummaryAsync(Guid userId, DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default);
}

public interface ICoverLetterDraftService
{
    Task<CoverLetterDraftResult> GenerateDraftAsync(CoverLetterDraftRequest request, CancellationToken cancellationToken = default);
}
