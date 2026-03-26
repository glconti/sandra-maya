using SandraMaya.Application.Abstractions;
using SandraMaya.Host.Assistant.ToolCalling;

namespace SandraMaya.Host.Assistant;

/// <summary>
/// Builds dynamic system prompts for Sandra Maya that include awareness
/// of available tools, user context (CV on file, tracked jobs, etc.),
/// and the assistant persona.
/// </summary>
public sealed class SystemPromptBuilder
{
    private readonly IMemoryQueryService _memoryQuery;
    private readonly ToolRegistry _toolRegistry;

    public SystemPromptBuilder(IMemoryQueryService memoryQuery, ToolRegistry toolRegistry)
    {
        _memoryQuery = memoryQuery;
        _toolRegistry = toolRegistry;
    }

    public async Task<string> BuildAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            CorePersona,
            RepoAreaSection,
            BuildToolSection(),
            await BuildUserContextAsync(userId, cancellationToken)
        };

        return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private const string CorePersona =
        """
        You are Sandra Maya, a personal AI assistant available through Telegram.
        You are helpful, resourceful, proactive, and friendly.
        You assist your user with job searching in the Zurich area (Switzerland), CV management,
        job application tracking, cover letter writing, web research, and any other personal tasks.

        Key behaviors:
        - Use your tools proactively. If the user asks about jobs, search and crawl. If they send a CV, ingest it.
        - Remember things by saving notes to memory. Retrieve context from memory when relevant.
        - Respond in the same language the user writes in.
        - When you cannot fulfill a request with the available tools or SDK-discovered skills, explain the limitation clearly.
        - Be concise but thorough. Prefer structured responses (bullet points, tables) for complex data.
        - When browsing the web, summarize what you find rather than dumping raw HTML.
        """;

    private const string RepoAreaSection =
        """
        Repository authoring areas:
        - Skill root: `src/SandraMaya.Host/Assistant/Skills`
        - Playwright helpers: `src/SandraMaya.Host/Playwright`
        - Host entrypoint reference: `src/SandraMaya.Host/Program.cs`
        - Host configuration reference: `src/SandraMaya.Host/Configuration`

        Treat those repo-relative paths as canonical aliases for repository work.
        Do not depend on machine-specific absolute paths because the checkout location may change.
        Write new skills under the skill root, keep shared browser helpers under the Playwright folder,
        and treat Program.cs and Configuration as read-mostly reference surfaces unless a code change
        is explicitly required.
        When a skill discovers jobs, persist them through `jobs_ingest_batch`.
        """;

    private string BuildToolSection()
    {
        var tools = _toolRegistry.GetAllHandlers();
        if (tools.Count == 0) return string.Empty;

        var lines = new List<string> { "You have the following tools available:" };
        foreach (var tool in tools)
        {
            lines.Add($"- **{tool.Name}**: {tool.Description}");
        }

        lines.Add("");
        lines.Add("Call tools whenever they would help answer the user's question. You can call multiple tools in sequence.");

        return string.Join("\n", lines);
    }

    private async Task<string> BuildUserContextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var parts = new List<string> { "Current user context:" };

        try
        {
            var cv = await _memoryQuery.GetCanonicalCvRevisionAsync(userId, cancellationToken);
            parts.Add(cv is not null
                ? $"- CV on file: yes (revision {cv.RevisionNumber}, uploaded {cv.UploadedAtUtc:yyyy-MM-dd})"
                : "- CV on file: no — ask the user to send their CV as a PDF");

            var jobs = await _memoryQuery.GetJobPostingsAsync(userId, cancellationToken);
            parts.Add($"- Saved job postings: {jobs.Count}");

            var applications = await _memoryQuery.GetJobApplicationStatusesAsync(userId, cancellationToken);
            parts.Add($"- Tracked applications: {applications.Count}");

            var profile = await _memoryQuery.GetAssistantProfileStateAsync(userId, cancellationToken);
            if (profile?.GoalsSummary is not null)
            {
                parts.Add($"- User goals: {profile.GoalsSummary}");
            }
        }
        catch
        {
            parts.Add("- (Could not load user context)");
        }

        return string.Join("\n", parts);
    }
}
