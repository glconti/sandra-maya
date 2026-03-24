using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Jobs;

public sealed partial class HostScriptedHttpJobCrawlStrategy : IJobCrawlStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJobCrawlIngestionService _ingestion;
    private readonly ILogger<HostScriptedHttpJobCrawlStrategy> _logger;

    public HostScriptedHttpJobCrawlStrategy(
        IHttpClientFactory httpClientFactory,
        IJobCrawlIngestionService ingestion,
        ILogger<HostScriptedHttpJobCrawlStrategy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ingestion = ingestion;
        _logger = logger;
    }

    public JobCrawlStrategyKind Kind => JobCrawlStrategyKind.ScriptedHttp;

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
            "Starting HTTP crawl for site {SiteKey} ({SearchUrl})",
            site.SiteKey, site.SearchUrl);

        try
        {
            var url = BuildSearchUrl(site.SearchUrl, request.Parameters);

            using var client = _httpClientFactory.CreateClient("JobCrawler");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            var html = await client.GetStringAsync(url, cancellationToken);

            var jobs = ExtractJobsFromHtml(html, site);

            _logger.LogInformation(
                "HTTP crawl for site {SiteKey} discovered {Count} job(s)",
                site.SiteKey, jobs.Count);

            var batch = new JobCrawlDiscoveryBatch
            {
                Request = request,
                Jobs = jobs,
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StrategyKind = Kind,
                RawBatchPayloadJson = JsonSerializer.Serialize(new { url, jobCount = jobs.Count })
            };

            return await _ingestion.ImportAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP crawl failed for site {SiteKey}", site.SiteKey);

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

    private static string BuildSearchUrl(string searchUrl, IReadOnlyDictionary<string, string> parameters)
    {
        var uriBuilder = new UriBuilder(searchUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

        if (parameters.TryGetValue("keywords", out var keywords) && !string.IsNullOrWhiteSpace(keywords))
            query["q"] = keywords;

        if (parameters.TryGetValue("location", out var location) && !string.IsNullOrWhiteSpace(location))
            query["location"] = location;

        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri.AbsoluteUri;
    }

    private static IReadOnlyList<DiscoveredJobPosting> ExtractJobsFromHtml(string html, JobSiteDefinition site)
    {
        var results = new List<DiscoveredJobPosting>();

        // Try JSON-LD structured data first (many job sites embed this)
        var jsonLdJobs = ExtractFromJsonLd(html, site);
        if (jsonLdJobs.Count > 0)
            return jsonLdJobs;

        // Fall back to HTML link extraction
        var linkMatches = LinkPattern().Matches(html);

        foreach (Match match in linkMatches)
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            var text = StripHtmlTags(WebUtility.HtmlDecode(match.Groups["text"].Value)).Trim();

            if (string.IsNullOrWhiteSpace(text) || text.Length < 5)
                continue;

            // Filter for job-like links
            if (!IsLikelyJobLink(href, text))
                continue;

            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(new Uri(site.BaseUrl), href, out var resolved))
                    href = resolved.AbsoluteUri;
                else
                    continue;
            }

            results.Add(new DiscoveredJobPosting
            {
                SourceUrl = href,
                Title = text,
                DedupeKey = href,
                RawPayloadJson = JsonSerializer.Serialize(new { href, text })
            });
        }

        // Deduplicate by URL
        return results
            .GroupBy(j => j.SourceUrl)
            .Select(g => g.First())
            .Take(50)
            .ToList();
    }

    private static IReadOnlyList<DiscoveredJobPosting> ExtractFromJsonLd(string html, JobSiteDefinition site)
    {
        var results = new List<DiscoveredJobPosting>();
        var scriptMatches = JsonLdPattern().Matches(html);

        foreach (Match scriptMatch in scriptMatches)
        {
            try
            {
                var jsonContent = scriptMatch.Groups["json"].Value;
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var items = GetJobPostingElements(root);
                foreach (var item in items)
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    if (string.IsNullOrWhiteSpace(url) && item.TryGetProperty("sameAs", out var sa))
                        url = sa.GetString();

                    url ??= site.SearchUrl;

                    var company = string.Empty;
                    if (item.TryGetProperty("hiringOrganization", out var org))
                    {
                        company = org.ValueKind == JsonValueKind.Object && org.TryGetProperty("name", out var n)
                            ? n.GetString() ?? string.Empty
                            : org.GetString() ?? string.Empty;
                    }

                    var loc = string.Empty;
                    if (item.TryGetProperty("jobLocation", out var jl))
                    {
                        if (jl.ValueKind == JsonValueKind.Object && jl.TryGetProperty("address", out var addr))
                        {
                            loc = addr.ValueKind == JsonValueKind.Object && addr.TryGetProperty("addressLocality", out var al)
                                ? al.GetString() ?? string.Empty
                                : addr.GetString() ?? string.Empty;
                        }
                    }

                    var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;

                    results.Add(new DiscoveredJobPosting
                    {
                        SourceUrl = url,
                        Title = title,
                        CompanyName = company,
                        Location = loc,
                        DescriptionPlainText = desc != null ? StripHtmlTags(desc) : null,
                        DedupeKey = url,
                        RawPayloadJson = item.GetRawText()
                    });
                }
            }
            catch (JsonException)
            {
                // Invalid JSON-LD block, skip
            }
        }

        return results;
    }

    private static IEnumerable<JsonElement> GetJobPostingElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                foreach (var item in GetJobPostingElements(el))
                    yield return item;
            }
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("@type", out var type))
        {
            var typeStr = type.GetString();
            if (string.Equals(typeStr, "JobPosting", StringComparison.OrdinalIgnoreCase))
            {
                yield return root;
                yield break;
            }
        }

        // Check @graph
        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in graph.EnumerateArray())
            {
                foreach (var item in GetJobPostingElements(el))
                    yield return item;
            }
        }
    }

    private static bool IsLikelyJobLink(string href, string text)
    {
        var hrefLower = href.ToLowerInvariant();
        var jobSegments = new[] { "job", "position", "vacancy", "career", "stelle", "stellenangebot" };

        return jobSegments.Any(s => hrefLower.Contains(s)) ||
               (text.Length > 10 && text.Length < 200 && !hrefLower.Contains("login") && !hrefLower.Contains("register"));
    }

    private static string StripHtmlTags(string input)
    {
        return HtmlTagPattern().Replace(input, " ").Trim();
    }

    [GeneratedRegex("""<a\s[^>]*href\s*=\s*["'](?<href>[^"']+)["'][^>]*>(?<text>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("""<script[^>]*type\s*=\s*["']application/ld\+json["'][^>]*>(?<json>.*?)</script>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex JsonLdPattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex HtmlTagPattern();
}
