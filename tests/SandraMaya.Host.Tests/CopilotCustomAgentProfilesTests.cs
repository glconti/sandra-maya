using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Tests;

public sealed class CopilotCustomAgentProfilesTests
{
    [Fact]
    public void Create_ReturnsDistinctJobSearchAgent()
    {
        var agents = CopilotCustomAgentProfiles.Create();

        Assert.Single(agents);

        var agent = agents[0];
        Assert.Equal(CopilotCustomAgentProfiles.JobSearchAgentName, agent.Name);
        Assert.Equal("Job Search", agent.DisplayName);
        Assert.True(agent.Infer);
        Assert.Contains("job discovery", agent.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dedicated job-search specialist", agent.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ConfiguresNarrowJobSearchToolset()
    {
        var agent = Assert.Single(CopilotCustomAgentProfiles.Create());
        var tools = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(agent.Tools);

        Assert.Equal(
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
            ],
            tools);

        Assert.DoesNotContain("memory_save_note", tools);
        Assert.DoesNotContain("memory_search", tools);
        Assert.DoesNotContain("web_browse", tools);
        Assert.DoesNotContain("web_search", tools);
        Assert.DoesNotContain("mcp_add_server", tools);
    }

    [Fact]
    public void MainAssistantDelegationSection_DefinesBoundaryWithJobSearchAgent()
    {
        var section = CopilotCustomAgentProfiles.MainAssistantDelegationSection;

        Assert.Contains("primary conversation owner", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("job-search", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delegate", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mixed-domain", section, StringComparison.OrdinalIgnoreCase);
    }
}
