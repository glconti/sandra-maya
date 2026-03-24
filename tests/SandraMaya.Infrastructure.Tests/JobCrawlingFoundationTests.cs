using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Infrastructure.Jobs;
using SandraMaya.Infrastructure.Persistence;

namespace SandraMaya.Infrastructure.Tests;

public sealed class JobCrawlingFoundationTests
{
    [Fact]
    public void JobSiteRegistry_ExposesSupportedSwissJobBoards()
    {
        var registry = new JobSiteRegistry();

        var sites = registry.GetAll();

        Assert.Equal(4, sites.Count);
        Assert.Contains(sites, static site => site.SiteKey == "jobs-ch");
        Assert.Contains(sites, static site => site.SiteKey == "jobagent-ch");
        Assert.Contains(sites, static site => site.SiteKey == "schuljobs-ch");
        Assert.Contains(sites, static site => site.SiteKey == "krippenstellen-ch");
        Assert.Equal("jobs-ch", registry.Find("JOBS-CH")!.SiteKey);
        Assert.Equal("https://www.schuljobs.ch/suche", registry.Find("schuljobs-ch")!.SearchUrl);
        Assert.Equal("https://krippenstellen.ch/de/inserate", registry.Find("krippenstellen-ch")!.SearchUrl);
        Assert.Equal(JobCrawlStrategyKind.PlaywrightBrowser, registry.Find("jobagent-ch")!.DefaultStrategy);
        Assert.Equal(JobCrawlStrategyKind.PlaywrightBrowser, registry.Find("krippenstellen-ch")!.DefaultStrategy);
    }

    [Fact]
    public void JobCrawlStrategySelectionService_PrefersPlaywrightForAuthenticatedSites()
    {
        var selector = new JobCrawlStrategySelectionService();
        var site = new JobSiteDefinition
        {
            SiteKey = "jobs-ch",
            AuthenticationMode = JobSiteAuthenticationMode.OptionalLogin,
            DefaultStrategy = JobCrawlStrategyKind.ScriptedHttp,
            SupportedStrategies =
            [
                JobCrawlStrategyKind.ScriptedHttp,
                JobCrawlStrategyKind.PlaywrightBrowser
            ]
        };

        var strategy = selector.Select(site, new JobCrawlRequest
        {
            UserProfileId = Guid.NewGuid(),
            SiteKey = "jobs-ch",
            Authentication = new JobCrawlAuthenticationContext
            {
                AccountKey = "saved-account"
            }
        });

        Assert.Equal(JobCrawlStrategyKind.PlaywrightBrowser, strategy);
    }

    [Fact]
    public async Task ImportAsync_UpsertsIntoMemoryStoreAndPreservesDedupe()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IJobCrawlIngestionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "crawl-ingestion"
        });

        var request = new JobCrawlRequest
        {
            UserProfileId = user.Id,
            SiteKey = "jobs-ch",
            Trigger = JobCrawlTriggerKind.ExternalImport
        };

        var firstResult = await ingestion.ImportAsync(new JobCrawlDiscoveryBatch
        {
            Request = request,
            StrategyKind = JobCrawlStrategyKind.ScriptedHttp,
            Jobs =
            [
                new DiscoveredJobPosting
                {
                    SourcePostingId = "123",
                    SourceUrl = "https://example.com/jobs/123",
                    Title = "Kindergarten Teacher",
                    CompanyName = "Zurich Schools",
                    Location = "Zurich",
                    EmploymentType = "Full-time",
                    DescriptionMarkdown = "# Role\nInitial description",
                    RawPayloadJson = """{"id":"123","title":"Kindergarten Teacher"}"""
                }
            ]
        });

        var secondResult = await ingestion.ImportAsync(new JobCrawlDiscoveryBatch
        {
            Request = request,
            StrategyKind = JobCrawlStrategyKind.ScriptedHttp,
            Jobs =
            [
                new DiscoveredJobPosting
                {
                    SourcePostingId = "123",
                    SourceUrl = "https://example.com/jobs/123?refresh=1",
                    Title = "Kindergarten Teacher",
                    CompanyName = "Zurich Schools",
                    Location = "Zurich",
                    EmploymentType = "Full-time",
                    DescriptionMarkdown = "# Role\nUpdated description",
                    RawPayloadJson = """{"id":"123","title":"Kindergarten Teacher","refresh":true}"""
                }
            ]
        });

        var postings = await queries.SearchJobPostingsAsync(user.Id, new JobPostingQuery(SearchText: "Teacher", ActiveOnly: false));
        var documents = await dbContext.MarkdownDocuments
            .AsNoTracking()
            .Where(x => x.UserProfileId == user.Id && x.Kind == DocumentKind.JobPosting)
            .ToListAsync();

        Assert.Single(firstResult.Items);
        Assert.Equal(JobCrawlItemStatus.Created, firstResult.Items[0].Status);
        Assert.Single(secondResult.Items);
        Assert.Equal(JobCrawlItemStatus.Updated, secondResult.Items[0].Status);
        Assert.Equal(1, secondResult.UpdatedCount);
        Assert.Single(postings);
        Assert.Equal("https://example.com/jobs/123?refresh=1", postings[0].SourceUrl);
        Assert.Single(documents);
        Assert.Contains("Updated description", documents[0].MarkdownContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_UsesFallbackCompanyWhenCrawlerCannotExtractEmployer()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IJobCrawlIngestionService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "crawl-missing-company"
        });

        var request = new JobCrawlRequest
        {
            UserProfileId = user.Id,
            SiteKey = "jobs-ch",
            Trigger = JobCrawlTriggerKind.ExternalImport
        };

        var result = await ingestion.ImportAsync(new JobCrawlDiscoveryBatch
        {
            Request = request,
            StrategyKind = JobCrawlStrategyKind.ScriptedHttp,
            Jobs =
            [
                new DiscoveredJobPosting
                {
                    SourceUrl = "https://example.com/jobs/unknown-company",
                    Title = "Primary Teacher",
                    CompanyName = "",
                    Location = "Zurich",
                    RawPayloadJson = """{"title":"Primary Teacher"}"""
                }
            ]
        });

        var postings = await queries.SearchJobPostingsAsync(
            user.Id,
            new JobPostingQuery(SearchText: "Primary Teacher", ActiveOnly: false));

        Assert.Equal(JobCrawlRunStatus.Succeeded, result.Status);
        Assert.Single(postings);
        Assert.Equal("Unknown employer", postings[0].CompanyName);
    }
}
