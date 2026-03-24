using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Infrastructure.Jobs;

public sealed class JobCrawlStrategySelectionService : IJobCrawlStrategySelector
{
    public JobCrawlStrategyKind Select(JobSiteDefinition site, JobCrawlRequest request)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestedStrategy is { } requestedStrategy)
        {
            if (!site.Supports(requestedStrategy))
            {
                throw new InvalidOperationException($"Site '{site.SiteKey}' does not support crawl strategy '{requestedStrategy}'.");
            }

            return requestedStrategy;
        }

        if (request.Authentication is not null &&
            site.AuthenticationMode != JobSiteAuthenticationMode.AnonymousOnly &&
            site.Supports(JobCrawlStrategyKind.PlaywrightBrowser))
        {
            return JobCrawlStrategyKind.PlaywrightBrowser;
        }

        if (site.Supports(site.DefaultStrategy))
        {
            return site.DefaultStrategy;
        }

        if (site.SupportedStrategies.Count > 0)
        {
            return site.SupportedStrategies[0];
        }

        throw new InvalidOperationException($"Site '{site.SiteKey}' does not declare any supported crawl strategies.");
    }
}
