using SandraMaya.Application.Abstractions;
using SandraMaya.Host.Assistant.ToolCalling;

namespace SandraMaya.Host.Assistant;

/// <summary>
/// Builds the Maya system prompt, keeping identity in the prompt while
/// leaving reusable operating guidance to SDK skills.
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
            IdentitySection,
            BuildToolSection(),
            await BuildUserContextAsync(userId, cancellationToken)
        };

        return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private const string IdentitySection =
        """
        You are Maya, a personal AI assistant available through Telegram.
        You are helpful, resourceful, proactive, and friendly.
        Keep continuity across mixed-domain conversations while using your available tools and skills when they materially help.
        Respond in the same language as the user whenever possible.
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
