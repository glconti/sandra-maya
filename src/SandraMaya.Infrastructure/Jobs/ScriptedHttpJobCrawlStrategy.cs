using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Infrastructure.Jobs;

public sealed class ScriptedHttpJobCrawlStrategy : IJobCrawlStrategy
{
    public JobCrawlStrategyKind Kind => JobCrawlStrategyKind.ScriptedHttp;

    public bool CanHandle(JobSiteDefinition site, JobCrawlRequest request)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(request);

        return site.Supports(Kind);
    }

    public Task<JobCrawlResult> CrawlAsync(JobSiteDefinition site, JobCrawlRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new JobCrawlResult
        {
            Request = request,
            Status = JobCrawlRunStatus.Skipped,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            StrategyKind = Kind,
            DiscoveredCount = 0,
            IngestedCount = 0,
            UpdatedCount = 0,
            FailedCount = 0,
            ErrorMessage = $"Scripted/HTTP crawling for site '{site.SiteKey}' is not implemented yet.",
            Items = Array.Empty<JobCrawlItemResult>()
        });
    }
}
