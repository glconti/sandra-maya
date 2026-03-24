using System.Buffers;
using System.Security.Cryptography;
using SandraMaya.Application.Abstractions;
using SandraMaya.Application.Contracts;
using SandraMaya.Infrastructure.Persistence;

namespace SandraMaya.Infrastructure.Storage;

internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string rootPath;

    internal LocalFileStorage(MemoryFoundationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        rootPath = Path.GetFullPath(paths.FileStorageRoot);
    }

    public async Task<StoredFileDescriptor> SaveAsync(FileStorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        Directory.CreateDirectory(rootPath);

        var timestamp = DateTime.UtcNow;
        var extension = Path.GetExtension(request.FileName);
        var relativeDirectory = Path.Combine(request.Partition.ToString().ToLowerInvariant(), timestamp.ToString("yyyy"), timestamp.ToString("MM"));
        var absoluteDirectory = Path.Combine(rootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, $"{timestamp:ddHHmmssfff}-{Guid.NewGuid():N}{extension}");
        var relativePath = Path.GetRelativePath(rootPath, absolutePath);

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        using var destination = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long totalBytes = 0;

        try
        {
            while (true)
            {
                var bytesRead = await request.Content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                incrementalHash.AppendData(buffer, 0, bytesRead);
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytes += bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var sha256 = Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
        return new StoredFileDescriptor(relativePath, absolutePath, request.ContentType, totalBytes, sha256);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = GetAbsolutePath(relativePath);
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public string GetAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(normalized)
            ? normalized
            : Path.GetFullPath(Path.Combine(rootPath, normalized));
    }
}
