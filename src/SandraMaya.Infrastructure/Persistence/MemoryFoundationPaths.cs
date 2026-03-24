using Microsoft.Extensions.Configuration;

namespace SandraMaya.Infrastructure.Persistence;

internal sealed record MemoryFoundationPaths(string SqlitePath, string FileStorageRoot)
{
    public static MemoryFoundationPaths FromConfiguration(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException("Content root path is required.", nameof(contentRootPath));
        }

        var storageSection = configuration.GetSection("Storage");
        var root = ResolveRoot(contentRootPath, storageSection["Root"] ?? "App_Data");

        return new MemoryFoundationPaths(
            ResolveChildPath(root, storageSection["SqlitePath"], "sqlite", "sandra-maya.db"),
            ResolveChildPath(root, storageSection["UploadsPath"], "files"));
    }

    private static string ResolveRoot(string contentRootPath, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, "App_Data"));
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(contentRootPath, configuredPath));
    }

    private static string ResolveChildPath(string root, string? configuredPath, params string[] fallbackSegments)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(Path.Combine(root, Path.Combine(fallbackSegments)));
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(root, configuredPath));
    }
}
