using System.Collections.Generic;

namespace SandraMaya.Application.Contracts;

public enum JobCrawlStrategyKind
{
    ScriptedHttp = 0,
    PlaywrightBrowser = 1
}

public enum JobCrawlTriggerKind
{
    Scheduled = 0,
    Manual = 1,
    ExternalImport = 2
}

public enum JobCrawlRunStatus
{
    Succeeded = 0,
    Skipped = 1,
    Failed = 2
}

public enum JobCrawlItemStatus
{
    Created = 0,
    Updated = 1,
    Failed = 2
}

public enum JobSiteAuthenticationMode
{
    AnonymousOnly = 0,
    OptionalLogin = 1,
    LoginRequired = 2
}

public sealed record JobSiteDefinition
{
    public string SiteKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string SearchUrl { get; init; } = string.Empty;
    public JobSiteAuthenticationMode AuthenticationMode { get; init; } = JobSiteAuthenticationMode.AnonymousOnly;
    public JobCrawlStrategyKind DefaultStrategy { get; init; } = JobCrawlStrategyKind.ScriptedHttp;
    public IReadOnlyList<JobCrawlStrategyKind> SupportedStrategies { get; init; } = Array.Empty<JobCrawlStrategyKind>();

    public bool Supports(JobCrawlStrategyKind strategy) => SupportedStrategies.Contains(strategy);
}

public sealed record JobCrawlAuthenticationContext
{
    public string? AccountKey { get; init; }
    public string? SessionReference { get; init; }
    public string? SecretReference { get; init; }
}

public sealed record JobCrawlRequest
{
    public Guid UserProfileId { get; init; }
    public string SiteKey { get; init; } = string.Empty;
    public JobCrawlTriggerKind Trigger { get; init; } = JobCrawlTriggerKind.Scheduled;
    public DateTimeOffset RequestedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }
    public JobCrawlStrategyKind? RequestedStrategy { get; init; }
    public string? ContinuationToken { get; init; }
    public JobCrawlAuthenticationContext? Authentication { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record DiscoveredJobPosting
{
    public string SourceUrl { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? SourcePostingId { get; init; }
    public string Location { get; init; } = string.Empty;
    public string EmploymentType { get; init; } = string.Empty;
    public string CompensationText { get; init; } = string.Empty;
    public string? DescriptionMarkdown { get; init; }
    public string? DescriptionPlainText { get; init; }
    public DateTimeOffset? PostedAtUtc { get; init; }
    public bool IsActive { get; init; } = true;
    public string RawPayloadJson { get; init; } = "{}";
    public string? DedupeKey { get; init; }
}

public sealed record JobCrawlDiscoveryBatch
{
    public required JobCrawlRequest Request { get; init; }
    public IReadOnlyList<DiscoveredJobPosting> Jobs { get; init; } = Array.Empty<DiscoveredJobPosting>();
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public JobCrawlStrategyKind? StrategyKind { get; init; }
    public string? ContinuationToken { get; init; }
    public string? RawBatchPayloadJson { get; init; }
}

public sealed record JobCrawlItemResult
{
    public string DedupeKey { get; init; } = string.Empty;
    public JobCrawlItemStatus Status { get; init; }
    public Guid? JobPostingId { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

public sealed record JobCrawlResult
{
    public required JobCrawlRequest Request { get; init; }
    public JobCrawlRunStatus Status { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public JobCrawlStrategyKind? StrategyKind { get; init; }
    public int DiscoveredCount { get; init; }
    public int IngestedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int FailedCount { get; init; }
    public string? ContinuationToken { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<JobCrawlItemResult> Items { get; init; } = Array.Empty<JobCrawlItemResult>();
}
