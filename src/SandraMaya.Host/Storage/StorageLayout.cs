using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Storage;

public sealed record StorageLayout(
    string Root,
    string SqlitePath,
    string UploadsPath,
    string RuntimeSkillsPath,
    string WorkPath,
    string TempPath)
{
    public static StorageLayout Create(StorageOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException("Content root path is required.", nameof(contentRootPath));
        }

        var root = ResolveRoot(contentRootPath, options.Root);
        return new StorageLayout(
            Root: root,
            SqlitePath: ResolveChildPath(root, options.SqlitePath, "sqlite", "sandra-maya.db"),
            UploadsPath: ResolveChildPath(root, options.UploadsPath, "files"),
            RuntimeSkillsPath: ResolveContentPath(contentRootPath, options.RuntimeSkillsPath, "Assistant", "Skills"),
            WorkPath: ResolveChildPath(root, options.WorkPath, "work"),
            TempPath: ResolveChildPath(root, options.TempPath, "tmp"));
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

    private static string ResolveContentPath(string contentRootPath, string? configuredPath, params string[] fallbackSegments)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, Path.Combine(fallbackSegments)));
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(contentRootPath, configuredPath));
    }
}
