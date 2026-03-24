using Microsoft.Extensions.DependencyInjection;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Tests;

public sealed class SqliteMemoryStoreTests
{
    [Fact]
    public async Task SaveCvRevisionAsync_UpdatesCanonicalRevision()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "single-user"
        });

        var firstAsset = await commands.SaveAssetMetadataAsync(new StoredAsset
        {
            UserProfileId = user.Id,
            Partition = StoragePartition.Uploads,
            Role = AssetRole.CvUpload,
            OriginalFileName = "cv-v1.pdf",
            ContentType = "application/pdf",
            StoragePath = @"uploads\cv-v1.pdf",
            ByteCount = 10,
            Sha256 = new string('a', 64)
        });

        var firstDocument = await commands.SaveMarkdownDocumentAsync(new MarkdownDocument
        {
            UserProfileId = user.Id,
            SourceAssetId = firstAsset.Id,
            Kind = DocumentKind.Cv,
            Title = "CV v1",
            MarkdownContent = """
# CV

Teacher profile
"""
        });

        await commands.SaveCvRevisionAsync(new CvRevision
        {
            UserProfileId = user.Id,
            SourceAssetId = firstAsset.Id,
            MarkdownDocumentId = firstDocument.Id,
            Summary = "Initial revision"
        });

        var secondAsset = await commands.SaveAssetMetadataAsync(new StoredAsset
        {
            UserProfileId = user.Id,
            Partition = StoragePartition.Uploads,
            Role = AssetRole.CvUpload,
            OriginalFileName = "cv-v2.pdf",
            ContentType = "application/pdf",
            StoragePath = @"uploads\cv-v2.pdf",
            ByteCount = 12,
            Sha256 = new string('b', 64)
        });

        var secondDocument = await commands.SaveMarkdownDocumentAsync(new MarkdownDocument
        {
            UserProfileId = user.Id,
            SourceAssetId = secondAsset.Id,
            Kind = DocumentKind.Cv,
            Title = "CV v2",
            MarkdownContent = """
# CV

Updated teacher profile
"""
        });

        await commands.SaveCvRevisionAsync(new CvRevision
        {
            UserProfileId = user.Id,
            SourceAssetId = secondAsset.Id,
            MarkdownDocumentId = secondDocument.Id,
            Summary = "Second revision"
        });

        var revisions = await queries.GetCvRevisionsAsync(user.Id);
        var canonical = await queries.GetCanonicalCvRevisionAsync(user.Id);

        Assert.Equal(2, revisions.Count);
        Assert.NotNull(canonical);
        Assert.Equal(2, canonical!.RevisionNumber);
        Assert.Equal(secondDocument.Id, canonical.MarkdownDocumentId);
        Assert.Single(revisions.Where(static revision => revision.IsCanonical));
    }

    [Fact]
    public async Task SearchDocumentsAsync_UsesFullTextIndex()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "memory-search"
        });

        await commands.SaveMarkdownDocumentAsync(new MarkdownDocument
        {
            UserProfileId = user.Id,
            Kind = DocumentKind.Cv,
            Title = "Teaching CV",
            MarkdownContent = """
# Profile

Experienced kindergarten teacher in Zurich
"""
        });

        await commands.SaveMarkdownDocumentAsync(new MarkdownDocument
        {
            UserProfileId = user.Id,
            Kind = DocumentKind.JobPosting,
            Title = "Software Role",
            MarkdownContent = """
# Vacancy

Senior .NET engineer role
"""
        });

        var results = await queries.SearchDocumentsAsync(user.Id, "kindergarten Zurich");

        Assert.Single(results);
        Assert.Equal("Teaching CV", results[0].Title);
        Assert.Equal(DocumentKind.Cv, results[0].Kind);
    }

    [Fact]
    public async Task UpsertJobPostingAsync_UsesDedupeKey()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "job-search"
        });

        var first = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "123",
            SourceUrl = "https://example.com/jobs/123",
            Title = "Kindergarten Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Full-time"
        });

        var second = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "123",
            SourceUrl = "https://example.com/jobs/123?updated=true",
            Title = "Kindergarten Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Full-time"
        });

        var results = await queries.SearchJobPostingsAsync(user.Id, new JobPostingQuery(SearchText: "Teacher"));

        Assert.Single(results);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("https://example.com/jobs/123?updated=true", results[0].SourceUrl);
    }

    [Fact]
    public async Task SaveJobApplicationStatusAsync_TracksLatestStatusPerPosting()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IMemoryCommandService>();
        var queries = scope.ServiceProvider.GetRequiredService<IMemoryQueryService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "application-tracking"
        });

        var posting = await commands.UpsertJobPostingAsync(new JobPosting
        {
            UserProfileId = user.Id,
            SourceSite = "jobs-ch",
            SourcePostingId = "456",
            SourceUrl = "https://example.com/jobs/456",
            Title = "Lead Teacher",
            CompanyName = "Zurich Schools",
            Location = "Zurich",
            EmploymentType = "Full-time"
        });

        var initial = await commands.SaveJobApplicationStatusAsync(new JobApplicationStatusRecord
        {
            UserProfileId = user.Id,
            JobPostingId = posting.Id,
            Status = JobApplicationStatus.Saved,
            NotesMarkdown = "Prepared notes."
        });

        var updated = await commands.SaveJobApplicationStatusAsync(new JobApplicationStatusRecord
        {
            UserProfileId = user.Id,
            JobPostingId = posting.Id,
            Status = JobApplicationStatus.Applied,
            NotesMarkdown = "Submitted application."
        });

        var statuses = await queries.GetJobApplicationStatusesAsync(user.Id);

        Assert.Single(statuses);
        Assert.Equal(initial.Id, updated.Id);
        Assert.Equal(JobApplicationStatus.Applied, statuses[0].Status);
        Assert.NotNull(statuses[0].AppliedAtUtc);
        Assert.Equal("Submitted application.", statuses[0].NotesMarkdown);
    }
}
