using Microsoft.Extensions.Logging.Abstractions;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Assistant.ToolCalling;

namespace SandraMaya.Host.Tests;

public sealed class AssistantSkillContentTests
{
    [Fact]
    public void RootSkill_DefinesDelegationBoundaryWithoutLegacyContradictions()
    {
        var skillPath = GetSkillPath();
        File.Exists(skillPath).Should().BeTrue();

        var content = File.ReadAllText(skillPath);

        content.Should().Contain("delegate that execution to the `job-search` specialist");
        content.Should().Contain("If you are the `job-search` specialist");
        content.Should().NotContain("You are Maya");
        content.Should().NotContain("You are Sandra Maya");
        content.Should().NotContain("If the user asks about jobs, search and crawl");
        content.Should().NotContain("When a skill discovers jobs, persist them through `jobs_ingest_batch`");
    }

    [Fact]
    public async Task BuildAsync_ReturnsOnlyDynamicPromptContent()
    {
        var memoryQuery = new StubMemoryQueryService
        {
            CanonicalCv = new CvRevision
            {
                RevisionNumber = 2,
                UploadedAtUtc = new DateTimeOffset(2026, 03, 28, 0, 0, 0, TimeSpan.Zero)
            },
            JobPostings =
            [
                new JobPosting(),
                new JobPosting()
            ],
            ApplicationStatuses =
            [
                new JobApplicationStatusRecord()
            ],
            ProfileState = new AssistantProfileState
            {
                GoalsSummary = "Find product engineering roles in Zurich."
            }
        };

        var registry = new ToolRegistry([], NullLogger<ToolRegistry>.Instance);
        var subject = new SystemPromptBuilder(memoryQuery, registry);

        var prompt = await subject.BuildAsync(Guid.NewGuid());

        prompt.Should().Contain("You are Maya, a personal AI assistant available through Telegram.");
        prompt.Should().Contain("Current user context:");
        prompt.Should().Contain("CV on file: yes (revision 2, uploaded 2026-03-28)");
        prompt.Should().Contain("Saved job postings: 2");
        prompt.Should().Contain("Tracked applications: 1");
        prompt.Should().Contain("User goals: Find product engineering roles in Zurich.");
        prompt.Should().NotContain("You are Sandra Maya");
        prompt.Should().NotContain("Repository authoring areas:");
        prompt.Should().NotContain("If the user asks about jobs, search and crawl");
        prompt.Should().NotContain("When a skill discovers jobs");
    }

    private static string GetSkillPath() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SandraMaya.Host", "Assistant", "Skills", "SKILL.md"));

    private sealed class StubMemoryQueryService : IMemoryQueryService
    {
        public AssistantProfileState? ProfileState { get; init; }

        public CvRevision? CanonicalCv { get; init; }

        public IReadOnlyList<JobPosting> JobPostings { get; init; } = [];

        public IReadOnlyList<JobApplicationStatusRecord> ApplicationStatuses { get; init; } = [];

        public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(null);

        public Task<AssistantProfileState?> GetAssistantProfileStateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProfileState);

        public Task<IReadOnlyList<StoredAsset>> GetAssetsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredAsset>>([]);

        public Task<IReadOnlyList<CvRevision>> GetCvRevisionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CvRevision>>([]);

        public Task<CvRevision?> GetCanonicalCvRevisionAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CanonicalCv);

        public Task<StructuredProfileSnapshot?> GetLatestStructuredProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StructuredProfileSnapshot?>(null);

        public Task<IReadOnlyList<MarkdownDocumentSearchResult>> SearchDocumentsAsync(Guid userId, string query, int limit = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarkdownDocumentSearchResult>>([]);

        public Task<IReadOnlyList<JobPosting>> SearchJobPostingsAsync(Guid userId, JobPostingQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobPosting>>([]);

        public Task<IReadOnlyList<JobPosting>> GetJobPostingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(JobPostings);

        public Task<JobPosting?> GetJobPostingAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<JobPosting?>(null);

        public Task<JobPosting?> GetJobPostingByDedupeKeyAsync(Guid userId, string dedupeKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<JobPosting?>(null);

        public Task<JobApplicationStatusRecord?> GetJobApplicationStatusAsync(Guid userId, Guid jobPostingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<JobApplicationStatusRecord?>(null);

        public Task<IReadOnlyList<JobApplicationStatusRecord>> GetJobApplicationStatusesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ApplicationStatuses);
    }
}
