namespace SandraMaya.Host.Storage;

public sealed class StorageBootstrapService(
    StorageLayout storageLayout,
    ILogger<StorageBootstrapService> logger) : IHostedService
{
    private readonly StorageLayout _storageLayout = storageLayout;
    private readonly ILogger<StorageBootstrapService> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var directoryPaths = new[]
        {
            _storageLayout.Root,
            Path.GetDirectoryName(_storageLayout.SqlitePath) ?? _storageLayout.Root,
            _storageLayout.UploadsPath,
            _storageLayout.RuntimeSkillsPath,
            _storageLayout.WorkPath,
            _storageLayout.TempPath
        };

        foreach (var path in directoryPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(path);
        }

        using (File.Open(_storageLayout.SqlitePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
        }

        _logger.LogInformation(
            "Storage initialized at {Root}. SQLite: {SqlitePath}; uploads: {UploadsPath}; skills: {RuntimeSkillsPath}",
            _storageLayout.Root,
            _storageLayout.SqlitePath,
            _storageLayout.UploadsPath,
            _storageLayout.RuntimeSkillsPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
