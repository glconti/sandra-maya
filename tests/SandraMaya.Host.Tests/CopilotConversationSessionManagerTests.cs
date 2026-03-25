using System.Collections.Concurrent;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Tests;

public sealed class CopilotConversationSessionManagerTests
{
    [Fact]
    public async Task OpenSessionAsync_RemovesInitializedMarker_WhenResumeAndRecreateFail()
    {
        var sessionId = "session-123";
        var store = new StaticAssistantSessionStore(sessionId);
        var client = new ThrowingCopilotSessionClient(
            resumeException: new InvalidOperationException("resume failed"),
            createException: new InvalidOperationException("create failed"));
        var provider = new TestCopilotClientProvider(client);
        var subject = new CopilotConversationSessionManager(provider, store, NullLogger<CopilotConversationSessionManager>.Instance);

        SeedInitializedSession(subject, sessionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.OpenSessionAsync(
                new ConversationReference(TransportPlatforms.Telegram, "chat-1", "user-1"),
                new SessionConfig(),
                new ResumeSessionConfig(),
                CancellationToken.None));

        Assert.False(IsInitialized(subject, sessionId));
        Assert.Equal(1, client.ResumeCallCount);
        Assert.Equal(1, client.CreateCallCount);
    }

    private static void SeedInitializedSession(CopilotConversationSessionManager subject, string sessionId)
    {
        GetInitializedSessions(subject).TryAdd(sessionId, 0);
    }

    private static bool IsInitialized(CopilotConversationSessionManager subject, string sessionId)
    {
        return GetInitializedSessions(subject).ContainsKey(sessionId);
    }

    private static ConcurrentDictionary<string, byte> GetInitializedSessions(CopilotConversationSessionManager subject)
    {
        var field = typeof(CopilotConversationSessionManager)
            .GetField("_initializedSessions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to access initialized session state.");

        return (ConcurrentDictionary<string, byte>)field.GetValue(subject)!;
    }

    private sealed class StaticAssistantSessionStore(string sessionId) : IAssistantSessionStore
    {
        private readonly AssistantSession _session = new(
            sessionId,
            new ConversationReference(TransportPlatforms.Telegram, "chat-1", "user-1"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        public Task<AssistantSession> GetOrCreateAsync(ConversationReference conversation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_session with { Conversation = conversation });
        }
    }

    private sealed class TestCopilotClientProvider(ICopilotSessionClient client) : ICopilotClientProvider
    {
        public ValueTask<ICopilotSessionClient> GetClientAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(client);
        }
    }

    private sealed class ThrowingCopilotSessionClient(
        Exception resumeException,
        Exception createException) : ICopilotSessionClient
    {
        public int ResumeCallCount { get; private set; }

        public int CreateCallCount { get; private set; }

        public Task<CopilotSession> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCallCount++;
            return Task.FromException<CopilotSession>(createException);
        }

        public Task<CopilotSession> ResumeSessionAsync(
            string sessionId,
            ResumeSessionConfig config,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResumeCallCount++;
            return Task.FromException<CopilotSession>(resumeException);
        }
    }
}
