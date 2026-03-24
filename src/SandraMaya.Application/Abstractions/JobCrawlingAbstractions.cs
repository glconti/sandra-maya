using SandraMaya.Application.Contracts;

namespace SandraMaya.Application.Abstractions;

public interface IJobSiteRegistry
{
    IReadOnlyList<JobSiteDefinition> GetAll();
    JobSiteDefinition? Find(string siteKey);
}

public interface IJobCrawlStrategySelector
{
    JobCrawlStrategyKind Select(JobSiteDefinition site, JobCrawlRequest request);
}

public interface IJobCrawlStrategy
{
    JobCrawlStrategyKind Kind { get; }
    bool CanHandle(JobSiteDefinition site, JobCrawlRequest request);
    Task<JobCrawlResult> CrawlAsync(JobSiteDefinition site, JobCrawlRequest request, CancellationToken cancellationToken = default);
}

public interface IJobCrawler
{
    Task<JobCrawlResult> CrawlAsync(JobCrawlRequest request, CancellationToken cancellationToken = default);
}

public interface IJobCrawlIngestionService
{
    Task<JobCrawlResult> ImportAsync(JobCrawlDiscoveryBatch batch, CancellationToken cancellationToken = default);
}
