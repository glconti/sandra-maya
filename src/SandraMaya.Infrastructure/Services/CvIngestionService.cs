using System.Text;
using System.Text.Json;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Application.Domain;

namespace SandraMaya.Infrastructure.Services;

public sealed class CvIngestionService(
    IFileStorage fileStorage,
    IPdfToMarkdownConverter pdfToMarkdownConverter,
    IMemoryCommandService memoryCommandService,
    IMemoryQueryService memoryQueryService) : ICvIngestionService
{
    public async Task<CvUploadIngestionResult> IngestAsync(CvUploadIngestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        var existingUser = await memoryQueryService.GetUserAsync(request.UserProfileId, cancellationToken);
        if (existingUser is null)
        {
            await memoryCommandService.SaveUserAsync(new UserProfile
            {
                Id = request.UserProfileId,
                ExternalUserKey = request.UserProfileId.ToString("N"),
                DisplayName = "Owner"
            }, cancellationToken);
        }

        var rawDescriptor = await fileStorage.SaveAsync(
            new FileStorageWriteRequest(request.FileName, request.ContentType, StoragePartition.Uploads, request.Content),
            cancellationToken);

        var rawAsset = await memoryCommandService.SaveAssetMetadataAsync(
            new StoredAsset
            {
                UserProfileId = request.UserProfileId,
                Partition = StoragePartition.Uploads,
                Role = AssetRole.CvUpload,
                OriginalFileName = request.FileName,
                ContentType = request.ContentType,
                StoragePath = rawDescriptor.RelativePath,
                ByteCount = rawDescriptor.ByteCount,
                Sha256 = rawDescriptor.Sha256
            },
            cancellationToken);

        await using var rawStream = await fileStorage.OpenReadAsync(rawDescriptor.RelativePath, cancellationToken);
        var conversion = await pdfToMarkdownConverter.ConvertAsync(rawStream, request.FileName, cancellationToken);
        var markdownContent = conversion.Markdown;

        await using var markdownStream = new MemoryStream(Encoding.UTF8.GetBytes(markdownContent));
        var markdownDescriptor = await fileStorage.SaveAsync(
            new FileStorageWriteRequest(Path.ChangeExtension(request.FileName, ".md"), "text/markdown", StoragePartition.Artifacts, markdownStream),
            cancellationToken);

        var markdownAsset = await memoryCommandService.SaveAssetMetadataAsync(
            new StoredAsset
            {
                UserProfileId = request.UserProfileId,
                Partition = StoragePartition.Artifacts,
                Role = AssetRole.MarkdownArtifact,
                DerivedFromAssetId = rawAsset.Id,
                OriginalFileName = Path.ChangeExtension(request.FileName, ".md"),
                ContentType = "text/markdown",
                StoragePath = markdownDescriptor.RelativePath,
                ByteCount = markdownDescriptor.ByteCount,
                Sha256 = markdownDescriptor.Sha256,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    conversion.Succeeded,
                    conversion.FailureReason
                })
            },
            cancellationToken);

        var markdownDocument = await memoryCommandService.SaveMarkdownDocumentAsync(
            new MarkdownDocument
            {
                UserProfileId = request.UserProfileId,
                SourceAssetId = rawAsset.Id,
                ArtifactAssetId = markdownAsset.Id,
                Kind = DocumentKind.Cv,
                Title = request.DocumentTitle ?? Path.GetFileNameWithoutExtension(request.FileName),
                MarkdownContent = markdownContent,
                PlainTextContent = conversion.PlainText,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    rawAssetId = rawAsset.Id,
                    markdownAssetId = markdownAsset.Id,
                    conversion.Succeeded,
                    conversion.FailureReason
                })
            },
            cancellationToken);

        var cvRevision = await memoryCommandService.SaveCvRevisionAsync(
            new CvRevision
            {
                UserProfileId = request.UserProfileId,
                SourceAssetId = rawAsset.Id,
                MarkdownDocumentId = markdownDocument.Id,
                Summary = request.RevisionSummary ?? $"Uploaded {request.FileName}",
                UploadedAtUtc = DateTimeOffset.UtcNow,
                IsCanonical = true
            },
            makeCanonical: true,
            cancellationToken);

        return new CvUploadIngestionResult(rawAsset, markdownAsset, markdownDocument, cvRevision);
    }
}
