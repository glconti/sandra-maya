using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Infrastructure.Jobs;

public sealed class JobCrawler(
    IJobSiteRegistry siteRegistry,
    IJobCrawlStrategySelector strategySelector,
    IEnumerable<IJobCrawlStrategy> strategies) : IJobCrawler
{
    private readonly IReadOnlyDictionary<JobCrawlStrategyKind, IJobCrawlStrategy> strategiesByKind = strategies.ToDictionary(static strategy => strategy.Kind);

    public Task<JobCrawlResult> CrawlAsync(JobCrawlRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = siteRegistry.Find(request.SiteKey)
            ?? throw new InvalidOperationException($"Unknown job site '{request.SiteKey}'.");

        var strategyKind = strategySelector.Select(site, request);
        if (!strategiesByKind.TryGetValue(strategyKind, out var strategy) || !strategy.CanHandle(site, request))
        {
            throw new InvalidOperationException($"No registered crawl strategy can handle site '{site.SiteKey}' with strategy '{strategyKind}'.");
        }

        return strategy.CrawlAsync(site, request, cancellationToken);
    }
}
