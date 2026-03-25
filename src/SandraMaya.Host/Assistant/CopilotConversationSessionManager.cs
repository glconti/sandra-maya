using System.Collections.Concurrent;
using GitHub.Copilot.SDK;

namespace SandraMaya.Host.Assistant;

public sealed class CopilotConversationSession : IAsyncDisposable
{
    public CopilotConversationSession(AssistantSession assistantSession, CopilotSession session)
    {
        AssistantSession = assistantSession;
        Session = session;
    }

    public AssistantSession AssistantSession { get; }

    public CopilotSession Session { get; }

    public ValueTask DisposeAsync() => Session.DisposeAsync();
}

public sealed class CopilotConversationSessionManager
{
    private readonly ConcurrentDictionary<string, byte> _initializedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICopilotClientProvider _clientProvider;
    private readonly IAssistantSessionStore _assistantSessionStore;
    private readonly ILogger<CopilotConversationSessionManager> _logger;

    public CopilotConversationSessionManager(
        ICopilotClientProvider clientProvider,
        IAssistantSessionStore assistantSessionStore,
        ILogger<CopilotConversationSessionManager> logger)
    {
        _clientProvider = clientProvider;
        _assistantSessionStore = assistantSessionStore;
        _logger = logger;
    }

    public async Task<CopilotConversationSession> OpenSessionAsync(
        ConversationReference conversation,
        SessionConfig createConfig,
        ResumeSessionConfig resumeConfig,
        CancellationToken cancellationToken)
    {
        var assistantSession = await _assistantSessionStore.GetOrCreateAsync(conversation, cancellationToken);
        var client = await _clientProvider.GetClientAsync(cancellationToken);

        var sessionId = assistantSession.SessionId;
        var shouldCreate = _initializedSessions.TryAdd(sessionId, 0);

        try
        {
            CopilotSession session;
            if (shouldCreate)
            {
                createConfig.SessionId = sessionId;
                session = await TryCreateThenResumeAsync(client, sessionId, createConfig, resumeConfig, cancellationToken);
            }
            else
            {
                session = await ResumeOrRecreateAsync(client, sessionId, createConfig, resumeConfig, cancellationToken);
            }

            return new CopilotConversationSession(assistantSession, session);
        }
        catch
        {
            _initializedSessions.TryRemove(sessionId, out _);
            throw;
        }
    }

    private async Task<CopilotSession> TryCreateThenResumeAsync(
        ICopilotSessionClient client,
        string sessionId,
        SessionConfig createConfig,
        ResumeSessionConfig resumeConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.CreateSessionAsync(createConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create Copilot session {SessionId}. Falling back to resume.",
                sessionId);

            return await client.ResumeSessionAsync(sessionId, resumeConfig, cancellationToken);
        }
    }

    private async Task<CopilotSession> ResumeOrRecreateAsync(
        ICopilotSessionClient client,
        string sessionId,
        SessionConfig createConfig,
        ResumeSessionConfig resumeConfig,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.ResumeSessionAsync(sessionId, resumeConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resume Copilot session {SessionId}. Recreating it.",
                sessionId);

            _initializedSessions.TryRemove(sessionId, out _);
            _initializedSessions.TryAdd(sessionId, 0);
            createConfig.SessionId = sessionId;
            return await client.CreateSessionAsync(createConfig, cancellationToken);
        }
    }
}
