using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Infrastructure.Jobs;

namespace SandraMaya.Infrastructure.Services;

public sealed class JobCrawlIngestionService(
    IJobSiteRegistry siteRegistry,
    IMemoryCommandService memoryCommands,
    IMemoryQueryService memoryQueries,
    JobPostingImportMapper mapper) : IJobCrawlIngestionService
{
    private const string UnknownEmployer = "Unknown employer";

    public async Task<JobCrawlResult> ImportAsync(JobCrawlDiscoveryBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(batch.Request);

        var site = siteRegistry.Find(batch.Request.SiteKey)
            ?? throw new InvalidOperationException($"Unknown job site '{batch.Request.SiteKey}'.");

        var completedAtUtc = batch.CompletedAtUtc ?? DateTimeOffset.UtcNow;
        var itemResults = new List<JobCrawlItemResult>(batch.Jobs.Count);
        var ingestedCount = 0;
        var updatedCount = 0;
        var failedCount = 0;

        foreach (var job in batch.Jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Validate(job);
                var normalizedJob = Normalize(job);

                var seedPosting = mapper.MapPosting(
                    batch.Request.UserProfileId,
                    site.SiteKey,
                    normalizedJob,
                    descriptionDocumentId: null,
                    lastSeenAtUtc: completedAtUtc);

                var existing = await memoryQueries.GetJobPostingByDedupeKeyAsync(
                    batch.Request.UserProfileId,
                    seedPosting.DedupeKey,
                    cancellationToken);

                var descriptionDocument = mapper.MapDescriptionDocument(
                    batch.Request.UserProfileId,
                    site.SiteKey,
                    normalizedJob,
                    existing?.DescriptionDocumentId);

                if (descriptionDocument is not null)
                {
                    descriptionDocument = await memoryCommands.SaveMarkdownDocumentAsync(descriptionDocument, cancellationToken);
                }

                var posting = mapper.MapPosting(
                    batch.Request.UserProfileId,
                    site.SiteKey,
                    normalizedJob,
                    descriptionDocument?.Id ?? existing?.DescriptionDocumentId,
                    completedAtUtc);

                if (existing is not null)
                {
                    posting.Id = existing.Id;
                    posting.FirstSeenAtUtc = existing.FirstSeenAtUtc;
                }

                var savedPosting = await memoryCommands.UpsertJobPostingAsync(posting, cancellationToken);
                var itemStatus = existing is null
                    ? JobCrawlItemStatus.Created
                    : JobCrawlItemStatus.Updated;

                ingestedCount++;
                if (itemStatus == JobCrawlItemStatus.Updated)
                {
                    updatedCount++;
                }

                itemResults.Add(new JobCrawlItemResult
                {
                    DedupeKey = savedPosting.DedupeKey,
                    Status = itemStatus,
                    JobPostingId = savedPosting.Id,
                    SourceUrl = savedPosting.SourceUrl
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedCount++;
                itemResults.Add(new JobCrawlItemResult
                {
                    DedupeKey = string.Empty,
                    Status = JobCrawlItemStatus.Failed,
                    SourceUrl = job.SourceUrl,
                    ErrorMessage = exception.Message
                });
            }
        }

        return new JobCrawlResult
        {
            Request = batch.Request,
            Status = failedCount > 0 && ingestedCount == 0
                ? JobCrawlRunStatus.Failed
                : JobCrawlRunStatus.Succeeded,
            StartedAtUtc = batch.StartedAtUtc,
            CompletedAtUtc = completedAtUtc,
            StrategyKind = batch.StrategyKind,
            DiscoveredCount = batch.Jobs.Count,
            IngestedCount = ingestedCount,
            UpdatedCount = updatedCount,
            FailedCount = failedCount,
            ContinuationToken = batch.ContinuationToken,
            Items = itemResults
        };
    }

    private static void Validate(DiscoveredJobPosting job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.SourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Title);
    }

    private static DiscoveredJobPosting Normalize(DiscoveredJobPosting job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return string.IsNullOrWhiteSpace(job.CompanyName)
            ? job with { CompanyName = UnknownEmployer }
            : job;
    }
}
