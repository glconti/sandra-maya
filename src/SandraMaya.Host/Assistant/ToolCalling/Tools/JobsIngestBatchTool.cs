using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;

namespace SandraMaya.Host.Assistant.ToolCalling.Tools;

public sealed class JobsIngestBatchTool(IJobCrawlIngestionService ingestionService) : IToolHandler
{
    public string Name => "jobs_ingest_batch";

    public string Description =>
        "Submit a batch of discovered jobs through the central ingestion pipeline so normalization, deduplication, document handling, and upserts stay consistent.";

    public BinaryData ParametersSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "site_key": {
                    "type": "string",
                    "description": "The registered site key that produced the discovered jobs."
                },
                "strategy_kind": {
                    "type": "string",
                    "enum": ["ScriptedHttp", "PlaywrightBrowser"],
                    "description": "Optional strategy used while discovering the jobs."
                },
                "correlation_id": {
                    "type": "string",
                    "description": "Optional correlation identifier for this import batch."
                },
                "continuation_token": {
                    "type": "string",
                    "description": "Optional continuation token returned by the upstream discovery process."
                },
                "started_at_utc": {
                    "type": "string",
                    "format": "date-time",
                    "description": "Optional ISO-8601 timestamp for when discovery started."
                },
                "completed_at_utc": {
                    "type": "string",
                    "format": "date-time",
                    "description": "Optional ISO-8601 timestamp for when discovery completed."
                },
                "raw_batch_payload_json": {
                    "type": "string",
                    "description": "Optional raw JSON payload captured from the upstream discovery process."
                },
                "parameters": {
                    "type": "object",
                    "description": "Optional string parameters associated with the discovery request.",
                    "additionalProperties": {
                        "type": "string"
                    }
                },
                "jobs": {
                    "type": "array",
                    "description": "Discovered jobs to ingest.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "source_url": { "type": "string" },
                            "title": { "type": "string" },
                            "company_name": { "type": "string" },
                            "source_posting_id": { "type": "string" },
                            "location": { "type": "string" },
                            "employment_type": { "type": "string" },
                            "compensation_text": { "type": "string" },
                            "description_markdown": { "type": "string" },
                            "description_plain_text": { "type": "string" },
                            "posted_at_utc": { "type": "string", "format": "date-time" },
                            "is_active": { "type": "boolean", "default": true },
                            "raw_payload_json": { "type": "string" },
                            "dedupe_key": { "type": "string" }
                        },
                        "required": ["source_url", "title"]
                    }
                }
            },
            "required": ["site_key", "jobs"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var siteKey = GetRequiredString(arguments, "site_key");
            var strategyKind = GetOptionalEnum<JobCrawlStrategyKind>(arguments, "strategy_kind");
            var request = new JobCrawlRequest
            {
                UserProfileId = context.UserId,
                SiteKey = siteKey,
                Trigger = JobCrawlTriggerKind.ExternalImport,
                RequestedAtUtc = DateTimeOffset.UtcNow,
                CorrelationId = GetOptionalString(arguments, "correlation_id"),
                RequestedStrategy = strategyKind,
                ContinuationToken = GetOptionalString(arguments, "continuation_token"),
                Parameters = GetOptionalStringDictionary(arguments, "parameters")
            };

            var batch = new JobCrawlDiscoveryBatch
            {
                Request = request,
                Jobs = GetJobs(arguments),
                StartedAtUtc = GetOptionalDateTimeOffset(arguments, "started_at_utc") ?? DateTimeOffset.UtcNow,
                CompletedAtUtc = GetOptionalDateTimeOffset(arguments, "completed_at_utc"),
                StrategyKind = strategyKind,
                ContinuationToken = GetOptionalString(arguments, "continuation_token"),
                RawBatchPayloadJson = GetOptionalString(arguments, "raw_batch_payload_json")
            };

            var result = await ingestionService.ImportAsync(batch, cancellationToken);
            return ToolResult.Json(result);
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to ingest discovered jobs: {ex.Message}");
        }
    }

    private static IReadOnlyList<DiscoveredJobPosting> GetJobs(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("jobs", out var jobsElement) || jobsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("jobs must be an array.");
        }

        var jobs = new List<DiscoveredJobPosting>();

        foreach (var jobElement in jobsElement.EnumerateArray())
        {
            if (jobElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Each jobs entry must be an object.");
            }

            jobs.Add(new DiscoveredJobPosting
            {
                SourceUrl = GetRequiredString(jobElement, "source_url"),
                Title = GetRequiredString(jobElement, "title"),
                CompanyName = GetOptionalString(jobElement, "company_name") ?? string.Empty,
                SourcePostingId = GetOptionalString(jobElement, "source_posting_id"),
                Location = GetOptionalString(jobElement, "location") ?? string.Empty,
                EmploymentType = GetOptionalString(jobElement, "employment_type") ?? string.Empty,
                CompensationText = GetOptionalString(jobElement, "compensation_text") ?? string.Empty,
                DescriptionMarkdown = GetOptionalString(jobElement, "description_markdown"),
                DescriptionPlainText = GetOptionalString(jobElement, "description_plain_text"),
                PostedAtUtc = GetOptionalDateTimeOffset(jobElement, "posted_at_utc"),
                IsActive = !jobElement.TryGetProperty("is_active", out var isActiveElement) || isActiveElement.GetBoolean(),
                RawPayloadJson = GetOptionalString(jobElement, "raw_payload_json") ?? "{}",
                DedupeKey = GetOptionalString(jobElement, "dedupe_key")
            });
        }

        return jobs;
    }

    private static IReadOnlyDictionary<string, string> GetOptionalStringDictionary(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (propertyElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{propertyName} must be an object.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in propertyElement.EnumerateObject())
        {
            values[property.Name] = property.Value.GetString()
                ?? throw new ArgumentException($"{propertyName}.{property.Name} must be a string.");
        }

        return values;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement) || string.IsNullOrWhiteSpace(propertyElement.GetString()))
        {
            throw new ArgumentException($"{propertyName} is required.");
        }

        return propertyElement.GetString()!;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var propertyElement)
            ? propertyElement.GetString()
            : null;

    private static DateTimeOffset? GetOptionalDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"{propertyName} must be a valid ISO-8601 date-time value.");
        }

        return parsed;
    }

    private static TEnum? GetOptionalEnum<TEnum>(JsonElement element, string propertyName)
        where TEnum : struct, Enum
    {
        var value = GetOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Unsupported {propertyName}: '{value}'.");
        }

        return parsed;
    }
}
