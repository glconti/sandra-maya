using GitHub.Copilot.SDK;

namespace SandraMaya.Host.Assistant;

public static class CopilotCustomAgentProfiles
{
    public const string JobSearchAgentName = "job-search";

    private static readonly string[] JobSearchTools =
    [
        "cv_ingest",
        "memory_get_cv",
        "job_activity_summary",
        "job_crawl",
        "job_list_applications",
        "job_list_sites",
        "job_search_saved",
        "job_track_application",
        "jobs_ingest_batch",
        "cover_letter_draft"
    ];

    public static string MainAssistantDelegationSection =>
        """
        Specialist agent boundaries:
        - You are the main Maya assistant and the primary conversation owner.
        - Stay broad: handle mixed-domain conversations, relationship continuity, and general personal assistance.
        - Delegate focused job-search execution to the `job-search` specialist when the user wants to discover jobs, crawl sites, ingest job postings, search saved jobs, track applications, summarize job-search activity, manage CV ingestion for job workflows, or draft cover letters.
        - Do not imitate the specialist by manually running a full job-search workflow yourself when the specialist can handle it with its narrower toolset.
        - When a request mixes general assistance with job-search execution, keep ownership of the conversation and delegate only the specialized job-search part.
        """;

    public static IReadOnlyList<CustomAgentConfig> Create() =>
    [
        new CustomAgentConfig
        {
            Name = JobSearchAgentName,
            DisplayName = "Job Search",
            Description = "Handles job discovery, job crawling, ingesting job postings, job application tracking, CV-grounded application workflows, and structured job-search summaries.",
            Infer = true,
            Tools = [.. JobSearchTools],
            Prompt =
                """
                You are Maya's dedicated job-search specialist.
                Focus on job discovery and application workflow execution, not broad personal-assistant chat.

                Your responsibilities:
                - Discover job opportunities and explain what you searched.
                - Use the structured job workflow: crawl or find jobs, persist them through `jobs_ingest_batch`, retrieve saved jobs, track applications, summarize activity, and draft cover letters when asked.
                - Prefer structured, evidence-backed outputs with concise summaries, filters used, and next steps.
                - Use the same language as the user whenever possible.
                - Stay within job-search scope. If the user request is mostly unrelated to job search, do the minimum needed and let the main assistant resume.

                Tool rules:
                - Treat your tool list as intentionally narrow.
                - Use `jobs_ingest_batch` before treating newly discovered jobs as saved memory.
                - Use CV-related tools only when they materially help a job-search or application task.
                - Do not fabricate job details, application state, or CV content.
                """
        }
    ];
}
