using Microsoft.Extensions.DependencyInjection;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;
using SandraMaya.Infrastructure.Services;

namespace SandraMaya.Infrastructure.Tests;

public sealed class CvIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_CreatesArtifactsAndCanonicalRevision()
    {
        await using var harness = await InfrastructureTestHarness.CreateAsync();
        await using var rootScope = harness.Services.CreateAsyncScope();
        var services = rootScope.ServiceProvider;
        var commands = services.GetRequiredService<IMemoryCommandService>();
        var queries = services.GetRequiredService<IMemoryQueryService>();

        var user = await commands.SaveUserAsync(new UserProfile
        {
            DisplayName = "Sandra",
            ExternalUserKey = "ingest-user"
        });

        var storage = services.GetRequiredService<IFileStorage>();
        var cvIngestionService = new CvIngestionService(
            storage,
            new FakePdfConverter(),
            commands,
            queries);

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var result = await cvIngestionService.IngestAsync(new CvUploadIngestionRequest(
            user.Id,
            "cv.pdf",
            "application/pdf",
            stream,
            RevisionSummary: "Seed CV"));

        var canonical = await queries.GetCanonicalCvRevisionAsync(user.Id);
        var assets = await queries.GetAssetsAsync(user.Id);

        Assert.NotNull(canonical);
        Assert.Equal(result.CvRevision.Id, canonical!.Id);
        Assert.Equal(2, assets.Count);
        Assert.Contains(assets, static asset => asset.Role == AssetRole.CvUpload);
        Assert.Contains(assets, static asset => asset.Role == AssetRole.MarkdownArtifact);
        Assert.Equal(DocumentKind.Cv, result.MarkdownDocument.Kind);
    }

    private sealed class FakePdfConverter : IPdfToMarkdownConverter
    {
        public Task<PdfToMarkdownResult> ConvertAsync(Stream pdfContent, string fileName, CancellationToken cancellationToken = default)
        {
            const string markdown = """
# CV

Experienced kindergarten teacher in Zurich
""";
            return Task.FromResult(new PdfToMarkdownResult(true, markdown, "Experienced kindergarten teacher in Zurich"));
        }
    }
}
