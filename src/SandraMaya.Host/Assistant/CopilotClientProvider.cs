using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Assistant;

public interface ICopilotClientProvider
{
    ValueTask<ICopilotSessionClient> GetClientAsync(CancellationToken cancellationToken);
}

public interface ICopilotSessionClient
{
    Task<CopilotSession> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken);

    Task<CopilotSession> ResumeSessionAsync(
        string sessionId,
        ResumeSessionConfig config,
        CancellationToken cancellationToken);
}

public sealed class CopilotClientProvider : ICopilotClientProvider, IAsyncDisposable
{
    private readonly CopilotRuntimeOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private CopilotClient? _client;

    public CopilotClientProvider(
        IOptions<CopilotRuntimeOptions> options,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _environment = environment;
        _loggerFactory = loggerFactory;
    }

    public async ValueTask<ICopilotSessionClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is { State: ConnectionState.Connected })
        {
            return new ConnectedCopilotSessionClient(_client);
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            _client ??= BuildClient();

            if (_client.State != ConnectionState.Connected)
            {
                await _client.StartAsync(cancellationToken);
            }

            return new ConnectedCopilotSessionClient(_client);
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _startLock.Dispose();
    }

    private CopilotClient BuildClient()
    {
        var clientOptions = new CopilotClientOptions
        {
            AutoStart = _options.AutoStart,
            Cwd = string.IsNullOrWhiteSpace(_options.WorkingDirectory)
                ? _environment.ContentRootPath
                : _options.WorkingDirectory!,
            GitHubToken = string.IsNullOrWhiteSpace(_options.GitHubToken)
                ? null
                : _options.GitHubToken,
            Logger = _loggerFactory.CreateLogger("GitHub.Copilot.SDK"),
            LogLevel = _options.LogLevel,
            UseLoggedInUser = _options.UseLoggedInUser,
            UseStdio = _options.UseStdio
        };

        if (!string.IsNullOrWhiteSpace(_options.CliPath))
        {
            clientOptions.CliPath = _options.CliPath;
        }

        return new CopilotClient(clientOptions);
    }

    private sealed class ConnectedCopilotSessionClient(CopilotClient client) : ICopilotSessionClient
    {
        public Task<CopilotSession> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken) =>
            client.CreateSessionAsync(config, cancellationToken);

        public Task<CopilotSession> ResumeSessionAsync(
            string sessionId,
            ResumeSessionConfig config,
            CancellationToken cancellationToken) =>
            client.ResumeSessionAsync(sessionId, config, cancellationToken);
    }
}
