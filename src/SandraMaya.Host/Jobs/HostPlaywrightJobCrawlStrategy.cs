using System.Text.Json;
using Microsoft.Extensions.Logging;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Host.Playwright;

namespace SandraMaya.Host.Jobs;

public sealed class HostPlaywrightJobCrawlStrategy : IJobCrawlStrategy
{
    private readonly IPlaywrightExecutionService _playwright;
    private readonly IJobCrawlIngestionService _ingestion;
    private readonly ILogger<HostPlaywrightJobCrawlStrategy> _logger;

    public HostPlaywrightJobCrawlStrategy(
        IPlaywrightExecutionService playwright,
        IJobCrawlIngestionService ingestion,
        ILogger<HostPlaywrightJobCrawlStrategy> logger)
    {
        _playwright = playwright;
        _ingestion = ingestion;
        _logger = logger;
    }

    public JobCrawlStrategyKind Kind => JobCrawlStrategyKind.PlaywrightBrowser;

    public bool CanHandle(JobSiteDefinition site, JobCrawlRequest request)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(request);
        return site.Supports(Kind);
    }

    public async Task<JobCrawlResult> CrawlAsync(
        JobSiteDefinition site,
        JobCrawlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Starting Playwright crawl for site {SiteKey} ({SearchUrl})",
            site.SiteKey, site.SearchUrl);

        try
        {
            request.Parameters.TryGetValue("keywords", out var keywords);
            request.Parameters.TryGetValue("location", out var location);

            var envVars = new Dictionary<string, string>
            {
                ["SEARCH_URL"] = site.SearchUrl,
                ["KEYWORDS"] = keywords ?? string.Empty,
                ["LOCATION"] = location ?? string.Empty
            };

            var scriptRequest = new PlaywrightScriptRequest
            {
                Script = BuildScript(),
                Timeout = TimeSpan.FromSeconds(60),
                EnvironmentVariables = envVars
            };

            var scriptResult = await _playwright.ExecuteScriptAsync(scriptRequest, cancellationToken);

            if (!scriptResult.Succeeded)
            {
                _logger.LogWarning(
                    "Playwright script failed for site {SiteKey}: exit={ExitCode}, error={Error}",
                    site.SiteKey, scriptResult.ExitCode, scriptResult.ErrorOutput);

                return new JobCrawlResult
                {
                    Request = request,
                    Status = JobCrawlRunStatus.Failed,
                    StartedAtUtc = startedAt,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    StrategyKind = Kind,
                    ErrorMessage = $"Playwright script failed (exit {scriptResult.ExitCode}): {scriptResult.ErrorOutput}",
                    Items = Array.Empty<JobCrawlItemResult>()
                };
            }

            var jobs = ParseOutput(scriptResult.Output, site);

            _logger.LogInformation(
                "Playwright crawl for site {SiteKey} discovered {Count} job(s)",
                site.SiteKey, jobs.Count);

            var batch = new JobCrawlDiscoveryBatch
            {
                Request = request,
                Jobs = jobs,
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StrategyKind = Kind,
                RawBatchPayloadJson = scriptResult.Output
            };

            return await _ingestion.ImportAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playwright crawl failed for site {SiteKey}", site.SiteKey);

            return new JobCrawlResult
            {
                Request = request,
                Status = JobCrawlRunStatus.Failed,
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StrategyKind = Kind,
                ErrorMessage = ex.Message,
                Items = Array.Empty<JobCrawlItemResult>()
            };
        }
    }

    private static IReadOnlyList<DiscoveredJobPosting> ParseOutput(string output, JobSiteDefinition site)
    {
        var trimmed = output.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Array.Empty<DiscoveredJobPosting>();

        // Find the JSON array in the output (skip any non-JSON preamble)
        var jsonStart = trimmed.IndexOf('[');
        var jsonEnd = trimmed.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            return Array.Empty<DiscoveredJobPosting>();

        var json = trimmed[jsonStart..(jsonEnd + 1)];

        using var doc = JsonDocument.Parse(json);
        var results = new List<DiscoveredJobPosting>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var title = element.TryGetProperty("title", out var t) ? t.GetString() : null;
            var url = element.TryGetProperty("url", out var u) ? u.GetString() : null;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                continue;

            // Resolve relative URLs against the site base
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(new Uri(site.BaseUrl), url, out var resolved))
            {
                url = resolved.AbsoluteUri;
            }

            var company = element.TryGetProperty("company", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            var loc = element.TryGetProperty("location", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            var desc = element.TryGetProperty("description", out var d) ? d.GetString() : null;

            results.Add(new DiscoveredJobPosting
            {
                SourceUrl = url,
                Title = title,
                CompanyName = company,
                Location = loc,
                DescriptionPlainText = desc,
                DedupeKey = url,
                RawPayloadJson = element.GetRawText()
            });
        }

        return results;
    }

    private static string BuildScript() =>
        """
        const { chromium } = require('playwright');
        (async () => {
          const browser = await chromium.launch({ headless: true });
          const page = await browser.newPage();
          const searchUrl = process.env.SEARCH_URL;
          const keywords = process.env.KEYWORDS || '';
          const location = process.env.LOCATION || '';

          await page.goto(searchUrl, { waitUntil: 'networkidle', timeout: 30000 });

          // Try to find and fill search inputs
          const searchInput = await page.$('input[name*="search"], input[name*="query"], input[name*="keyword"], input[type="search"], input[placeholder*="search" i], input[placeholder*="job" i]');
          if (searchInput && keywords) {
            await searchInput.fill(keywords);
          }

          const locationInput = await page.$('input[name*="location"], input[name*="ort"], input[name*="city"], input[placeholder*="location" i], input[placeholder*="ort" i]');
          if (locationInput && location) {
            await locationInput.fill(location);
          }

          // Submit if we filled something
          if (searchInput && keywords) {
            await page.keyboard.press('Enter');
            await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
          }

          // Wait a bit for dynamic content
          await page.waitForTimeout(3000);

          // Extract job listings - try multiple common selectors
          const jobs = await page.evaluate(() => {
            const selectors = [
              'article', '[class*="job"]', '[class*="listing"]', '[class*="vacancy"]',
              '[class*="result"]', '[data-job]', '.job-item', '.job-card',
              'li[class*="job"]', 'div[class*="card"]'
            ];

            let items = [];
            for (const sel of selectors) {
              const els = document.querySelectorAll(sel);
              if (els.length > 2 && els.length < 200) {
                items = Array.from(els);
                break;
              }
            }

            return items.slice(0, 50).map(el => {
              const link = el.querySelector('a[href]');
              const title = el.querySelector('h2, h3, h4, [class*="title"]');
              const company = el.querySelector('[class*="company"], [class*="employer"], [class*="firma"]');
              const loc = el.querySelector('[class*="location"], [class*="ort"], [class*="city"]');
              const desc = el.querySelector('[class*="description"], [class*="snippet"], [class*="text"], p');

              return {
                title: title?.textContent?.trim() || el.querySelector('a')?.textContent?.trim() || '',
                company: company?.textContent?.trim() || '',
                location: loc?.textContent?.trim() || '',
                url: link?.href || '',
                description: desc?.textContent?.trim() || ''
              };
            }).filter(j => j.title && j.url);
          });

          console.log(JSON.stringify(jobs));
          await browser.close();
        })();
        """;
}
