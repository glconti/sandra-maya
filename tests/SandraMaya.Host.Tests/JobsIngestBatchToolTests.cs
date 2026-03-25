using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Host.Assistant.ToolCalling;
using SandraMaya.Host.Assistant.ToolCalling.Tools;

namespace SandraMaya.Host.Tests;

public sealed class JobsIngestBatchToolTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsBatchToIngestionService()
    {
        var ingestion = new RecordingJobCrawlIngestionService();
        var subject = new JobsIngestBatchTool(ingestion);
        var userId = Guid.NewGuid();

        var result = await subject.ExecuteAsync(
            Parse("""
                {
                  "site_key": "jobs.ch",
                  "strategy_kind": "PlaywrightBrowser",
                  "correlation_id": "corr-123",
                  "continuation_token": "next-page",
                  "started_at_utc": "2026-03-25T12:00:00Z",
                  "completed_at_utc": "2026-03-25T12:05:00Z",
                  "raw_batch_payload_json": "{\"page\":1}",
                  "parameters": {
                    "query": "software engineer",
                    "location": "Zurich"
                  },
                  "jobs": [
                    {
                      "source_url": "https://example.test/jobs/1",
                      "title": "Software Engineer",
                      "company_name": "Acme",
                      "location": "Zurich",
                      "description_markdown": "Great role",
                      "description_plain_text": "Great role",
                      "is_active": true,
                      "raw_payload_json": "{\"id\":1}"
                    }
                  ]
                }
                """),
            new ToolExecutionContext(userId, "session-1", null));

        Assert.False(result.IsError);
        Assert.NotNull(ingestion.LastBatch);
        Assert.Equal(userId, ingestion.LastBatch!.Request.UserProfileId);
        Assert.Equal("jobs.ch", ingestion.LastBatch.Request.SiteKey);
        Assert.Equal(JobCrawlTriggerKind.ExternalImport, ingestion.LastBatch.Request.Trigger);
        Assert.Equal(JobCrawlStrategyKind.PlaywrightBrowser, ingestion.LastBatch.Request.RequestedStrategy);
        Assert.Equal("corr-123", ingestion.LastBatch.Request.CorrelationId);
        Assert.Equal("next-page", ingestion.LastBatch.Request.ContinuationToken);
        Assert.Equal("software engineer", ingestion.LastBatch.Request.Parameters["query"]);
        Assert.Equal("Zurich", ingestion.LastBatch.Request.Parameters["location"]);
        Assert.Equal(JobCrawlStrategyKind.PlaywrightBrowser, ingestion.LastBatch.StrategyKind);
        Assert.Equal(DateTimeOffset.Parse("2026-03-25T12:00:00Z"), ingestion.LastBatch.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-03-25T12:05:00Z"), ingestion.LastBatch.CompletedAtUtc);
        Assert.Equal("{\"page\":1}", ingestion.LastBatch.RawBatchPayloadJson);
        Assert.Single(ingestion.LastBatch.Jobs);
        Assert.Equal("Software Engineer", ingestion.LastBatch.Jobs[0].Title);
        Assert.Equal("Acme", ingestion.LastBatch.Jobs[0].CompanyName);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class RecordingJobCrawlIngestionService : IJobCrawlIngestionService
    {
        public JobCrawlDiscoveryBatch? LastBatch { get; private set; }

        public Task<JobCrawlResult> ImportAsync(JobCrawlDiscoveryBatch batch, CancellationToken cancellationToken = default)
        {
            LastBatch = batch;

            return Task.FromResult(new JobCrawlResult
            {
                Request = batch.Request,
                Status = JobCrawlRunStatus.Succeeded,
                StartedAtUtc = batch.StartedAtUtc,
                CompletedAtUtc = batch.CompletedAtUtc ?? batch.StartedAtUtc,
                StrategyKind = batch.StrategyKind,
                DiscoveredCount = batch.Jobs.Count,
                IngestedCount = batch.Jobs.Count,
                UpdatedCount = 0,
                FailedCount = 0,
                ContinuationToken = batch.ContinuationToken,
                Items = []
            });
        }
    }
}
