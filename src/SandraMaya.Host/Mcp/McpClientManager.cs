using System.Text.Json;
using System.Text.Json.Serialization;
using SandraMaya.Host.Storage;

namespace SandraMaya.Host.Mcp;

/// <summary>
/// Manages MCP server configuration. This is a stub implementation that handles
/// config persistence only — actual MCP protocol connections will be added later
/// when the ModelContextProtocol NuGet package is integrated.
/// </summary>
public sealed class McpClientManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;
    private readonly ILogger<McpClientManager> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public McpClientManager(StorageLayout storageLayout, ILogger<McpClientManager> logger)
    {
        _configPath = Path.Combine(storageLayout.Root, "mcp-servers.json");
        _logger = logger;
    }

    public async Task<IReadOnlyList<McpServerConfiguration>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        return config.Servers.AsReadOnly();
    }

    public async Task<McpServerConfiguration> AddServerAsync(McpServerConfiguration server, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var config = await LoadConfigAsync(cancellationToken);

            var existing = config.Servers.FindIndex(s =>
                string.Equals(s.Id, server.Id, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                config.Servers[existing] = server;
                _logger.LogInformation("Updated existing MCP server configuration: {ServerId}", server.Id);
            }
            else
            {
                config.Servers.Add(server);
                _logger.LogInformation("Added new MCP server configuration: {ServerId}", server.Id);
            }

            await SaveConfigAsync(config, cancellationToken);

            _logger.LogWarning(
                "MCP server connection not yet implemented, server '{ServerId}' registered for future use.",
                server.Id);

            return server;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> RemoveServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var config = await LoadConfigAsync(cancellationToken);
            var removed = config.Servers.RemoveAll(s =>
                string.Equals(s.Id, serverId, StringComparison.OrdinalIgnoreCase)) > 0;

            if (removed)
            {
                await SaveConfigAsync(config, cancellationToken);
                _logger.LogInformation("Removed MCP server configuration: {ServerId}", serverId);
            }

            return removed;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<McpServersConfig> LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return new McpServersConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            return JsonSerializer.Deserialize<McpServersConfig>(json, JsonOptions) ?? new McpServersConfig();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read MCP config from {Path}, returning empty config.", _configPath);
            return new McpServersConfig();
        }
    }

    private async Task SaveConfigAsync(McpServersConfig config, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_configPath, json, cancellationToken);
    }
}
