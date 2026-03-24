using Microsoft.Extensions.DependencyInjection;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Tests;

public sealed class JobTrackingServicesTests
{
    [Fact]
    public async Task MarkStatusAsync_SavesInterestedStateAndReturnsCurrentSnapshot()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var tracking = scope.ServiceProvider.GetRequiredService<IJobApplicationTrackingService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "tracking-service",
            TimeZone = "UTC"
        });

        var posting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "track-1",
            SourceUrl = "https://example.com/jobs/track-1",
            Title = "Classroom Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Full-time"
        });

        var current = await tracking.MarkStatusAsync(new JobApplicationStatusUpdateRequest(
            user.Id,
            posting.Id,
            JobApplicationStatus.Interested,
            NotesMarkdown: "Worth reviewing with Sandra before applying."));

        Assert.True(current.IsTracked);
        Assert.Equal(JobApplicationStatus.Interested, current.CurrentStatus);
        Assert.Equal(posting.Id, current.JobPosting.Id);
        Assert.Equal("Worth reviewing with Sandra before applying.", current.StatusRecord!.NotesMarkdown);
    }

    [Fact]
    public async Task ListApplicationsAsync_FiltersByRequestedStatuses()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var tracking = scope.ServiceProvider.GetRequiredService<IJobApplicationTrackingService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "tracking-filter",
            TimeZone = "UTC"
        });

        var interestedPosting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "track-2",
            SourceUrl = "https://example.com/jobs/track-2",
            Title = "Assistant Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Part-time"
        });

        var rejectedPosting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "track-3",
            SourceUrl = "https://example.com/jobs/track-3",
            Title = "Lead Teacher",
            CompanyName = "Bern Schools",
            Location = "Bern",
            EmploymentType = "Full-time"
        });

        await tracking.MarkStatusAsync(new JobApplicationStatusUpdateRequest(
            user.Id,
            interestedPosting.Id,
            JobApplicationStatus.Interested,
            NotesMarkdown: "Keep on shortlist."));

        await tracking.MarkStatusAsync(new JobApplicationStatusUpdateRequest(
            user.Id,
            rejectedPosting.Id,
            JobApplicationStatus.Rejected,
            NotesMarkdown: "Closed by employer."));

        var interestedOnly = await tracking.ListApplicationsAsync(
            user.Id,
            new JobApplicationListQuery(new[] { JobApplicationStatus.Interested }));

        Assert.Single(interestedOnly);
        Assert.Equal(interestedPosting.Id, interestedOnly[0].JobPosting.Id);
        Assert.Equal(JobApplicationStatus.Interested, interestedOnly[0].CurrentStatus);
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_AggregatesDiscoveryAndApplicationActivity()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var reporting = scope.ServiceProvider.GetRequiredService<IJobActivityReportingService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "reporting-weekly",
            TimeZone = "UTC"
        });

        var appliedPosting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "weekly-1",
            SourceUrl = "https://example.com/jobs/weekly-1",
            Title = "Homeroom Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Full-time",
            FirstSeenAtUtc = new DateTimeOffset(2024, 5, 14, 9, 0, 0, TimeSpan.Zero),
            LastSeenAtUtc = new DateTimeOffset(2024, 5, 14, 9, 0, 0, TimeSpan.Zero)
        });

        var rejectedPosting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "weekly-2",
            SourceUrl = "https://example.com/jobs/weekly-2",
            Title = "Support Teacher",
            CompanyName = "Winterthur Schools",
            Location = "Winterthur",
            EmploymentType = "Part-time",
            FirstSeenAtUtc = new DateTimeOffset(2024, 5, 15, 9, 0, 0, TimeSpan.Zero),
            LastSeenAtUtc = new DateTimeOffset(2024, 5, 15, 9, 0, 0, TimeSpan.Zero)
        });

        var interviewingPosting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "weekly-3",
            SourceUrl = "https://example.com/jobs/weekly-3",
            Title = "STEM Teacher",
            CompanyName = "Basel Schools",
            Location = "Basel",
            EmploymentType = "Full-time",
            FirstSeenAtUtc = new DateTimeOffset(2024, 5, 1, 9, 0, 0, TimeSpan.Zero),
            LastSeenAtUtc = new DateTimeOffset(2024, 5, 15, 9, 0, 0, TimeSpan.Zero)
        });

        await commands.SaveJobApplicationStatusAsync(new JobApplicationStatusRecord
        {
            UserProfileId = user.Id,
            JobPostingId = appliedPosting.Id,
            Status = JobApplicationStatus.Applied,
            CreatedAtUtc = new DateTimeOffset(2024, 5, 14, 10, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2024, 5, 14, 11, 0, 0, TimeSpan.Zero),
            AppliedAtUtc = new DateTimeOffset(2024, 5, 14, 11, 0, 0, TimeSpan.Zero),
            NotesMarkdown = "Application ready."
        });

        await commands.SaveJobApplicationStatusAsync(new JobApplicationStatusRecord
        {
            UserProfileId = user.Id,
            JobPostingId = rejectedPosting.Id,
            Status = JobApplicationStatus.Rejected,
            CreatedAtUtc = new DateTimeOffset(2024, 5, 15, 8, 30, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2024, 5, 15, 12, 0, 0, TimeSpan.Zero),
            NotesMarkdown = "Employer responded with a rejection."
        });

        await commands.SaveJobApplicationStatusAsync(new JobApplicationStatusRecord
        {
            UserProfileId = user.Id,
            JobPostingId = interviewingPosting.Id,
            Status = JobApplicationStatus.Interviewing,
            CreatedAtUtc = new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2024, 5, 15, 14, 0, 0, TimeSpan.Zero),
            AppliedAtUtc = new DateTimeOffset(2024, 5, 2, 9, 0, 0, TimeSpan.Zero),
            NotesMarkdown = "Interview scheduled."
        });

        var summary = await reporting.GetWeeklySummaryAsync(
            user.Id,
            new DateTimeOffset(2024, 5, 15, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal(JobActivitySummaryPeriod.Weekly, summary.Period);
        Assert.Equal(new DateTimeOffset(2024, 5, 13, 0, 0, 0, TimeSpan.Zero), summary.RangeStartUtc);
        Assert.Equal(new DateTimeOffset(2024, 5, 20, 0, 0, 0, TimeSpan.Zero), summary.RangeEndUtc);
        Assert.Equal(2, summary.JobsDiscovered);
        Assert.Equal(2, summary.JobsTracked);
        Assert.Equal(1, summary.ApplicationsSubmitted);
        Assert.Equal(1, summary.InterviewsAdvanced);
        Assert.Equal(1, summary.RejectionsLogged);
        Assert.Contains(summary.CurrentStatusCounts, count => count.Status == JobApplicationStatus.Applied && count.Count == 1);
        Assert.Contains(summary.CurrentStatusCounts, count => count.Status == JobApplicationStatus.Rejected && count.Count == 1);
        Assert.Contains(summary.CurrentStatusCounts, count => count.Status == JobApplicationStatus.Interviewing && count.Count == 1);
        Assert.Equal(2, summary.RecentDiscoveries.Count);
        Assert.Equal(3, summary.RecentApplicationUpdates.Count);
    }

    [Fact]
    public async Task GenerateDraftAsync_UsesCanonicalCvAndJobPostingContext()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var coverLetters = scope.ServiceProvider.GetRequiredService<ICoverLetterDraftService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "cover-letter",
            TimeZone = "UTC"
        });

        var cvAsset = await commands.SaveAssetMetadataAsync(new StoredAsset
        {
            UserProfileId = user.Id,
            Partition = StoragePartition.Uploads,
            Role = AssetRole.CvUpload,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StoragePath = @"uploads\cv.pdf",
            ByteCount = 20,
            Sha256 = new string('c', 64)
        });

        var cvDocument = await commands.SaveMarkdownDocumentAsync(new MarkdownDocument
        {
            UserProfileId = user.Id,
            SourceAssetId = cvAsset.Id,
            Kind = DocumentKind.Cv,
            Title = "Canonical CV",
            MarkdownContent = """
                # Sandra Maya CV

                Experienced teacher and mentor.
                """
        });

        var cvRevision = await commands.SaveCvRevisionAsync(new CvRevision
        {
            UserProfileId = user.Id,
            SourceAssetId = cvAsset.Id,
            MarkdownDocumentId = cvDocument.Id,
            Summary = "Experienced teacher and mentor.",
            UploadedAtUtc = new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero)
        });

        var posting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "cover-1",
            SourceUrl = "https://example.com/jobs/cover-1",
            Title = "Primary School Teacher",
            CompanyName = "Lucerne Schools",
            Location = "Lucerne",
            EmploymentType = "Full-time"
        });

        var draft = await coverLetters.GenerateDraftAsync(new CoverLetterDraftRequest(
            user.Id,
            posting.Id,
            AdditionalGuidance: "Focus on classroom leadership.",
            Tone: "warm",
            Language: "en"));

        Assert.True(draft.IsPlaceholder);
        Assert.Equal(cvRevision.Id, draft.CvRevisionId);
        Assert.Equal("Primary School Teacher", draft.JobTitle);
        Assert.Equal("Lucerne Schools", draft.CompanyName);
        Assert.Contains("Focus on classroom leadership.", draft.DraftMarkdown);
        Assert.Contains(posting.SourceUrl, draft.DraftMarkdown);
        Assert.Contains(cvRevision.Id.ToString(), draft.PromptHint);
        Assert.Contains(posting.Id.ToString(), draft.PromptHint);
    }
}
