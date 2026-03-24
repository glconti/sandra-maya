using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Infrastructure.Jobs;

public sealed class JobSiteRegistry : IJobSiteRegistry
{
    private static readonly JobSiteDefinition[] Sites =
    [
        new()
        {
            SiteKey = "jobs-ch",
            DisplayName = "jobs.ch",
            BaseUrl = "https://www.jobs.ch/",
            SearchUrl = "https://www.jobs.ch/en/vacancies/",
            AuthenticationMode = JobSiteAuthenticationMode.OptionalLogin,
            DefaultStrategy = JobCrawlStrategyKind.ScriptedHttp,
            SupportedStrategies =
            [
                JobCrawlStrategyKind.ScriptedHttp,
                JobCrawlStrategyKind.PlaywrightBrowser
            ]
        },
        new()
        {
            SiteKey = "jobagent-ch",
            DisplayName = "jobagent.ch",
            BaseUrl = "https://www.jobagent.ch/",
            SearchUrl = "https://www.jobagent.ch/jobs",
            AuthenticationMode = JobSiteAuthenticationMode.OptionalLogin,
            DefaultStrategy = JobCrawlStrategyKind.ScriptedHttp,
            SupportedStrategies =
            [
                JobCrawlStrategyKind.ScriptedHttp,
                JobCrawlStrategyKind.PlaywrightBrowser
            ]
        },
        new()
        {
            SiteKey = "schuljobs-ch",
            DisplayName = "schuljobs.ch",
            BaseUrl = "https://www.schuljobs.ch/",
            SearchUrl = "https://www.schuljobs.ch/offene-stellen",
            AuthenticationMode = JobSiteAuthenticationMode.AnonymousOnly,
            DefaultStrategy = JobCrawlStrategyKind.ScriptedHttp,
            SupportedStrategies =
            [
                JobCrawlStrategyKind.ScriptedHttp,
                JobCrawlStrategyKind.PlaywrightBrowser
            ]
        },
        new()
        {
            SiteKey = "krippenstellen-ch",
            DisplayName = "krippenstellen.ch",
            BaseUrl = "https://www.krippenstellen.ch/",
            SearchUrl = "https://www.krippenstellen.ch/offene-stellen",
            AuthenticationMode = JobSiteAuthenticationMode.AnonymousOnly,
            DefaultStrategy = JobCrawlStrategyKind.ScriptedHttp,
            SupportedStrategies =
            [
                JobCrawlStrategyKind.ScriptedHttp,
                JobCrawlStrategyKind.PlaywrightBrowser
            ]
        }
    ];

    private readonly IReadOnlyDictionary<string, JobSiteDefinition> sitesByKey = Sites.ToDictionary(static site => site.SiteKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<JobSiteDefinition> GetAll() => Sites;

    public JobSiteDefinition? Find(string siteKey)
    {
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return null;
        }

        return sitesByKey.TryGetValue(siteKey.Trim(), out var site)
            ? site
            : null;
    }
}
