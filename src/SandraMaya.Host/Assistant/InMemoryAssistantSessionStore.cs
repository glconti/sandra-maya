using System.Collections.Concurrent;

namespace SandraMaya.Host.Assistant;

public sealed class InMemoryAssistantSessionStore : IAssistantSessionStore
{
    private readonly ConcurrentDictionary<string, AssistantSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public Task<AssistantSession> GetOrCreateAsync(
        ConversationReference conversation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = $"{conversation.Platform}:{conversation.ConversationId}";
        var now = DateTimeOffset.UtcNow;

        var session = _sessions.AddOrUpdate(
            key,
            _ => new AssistantSession(Guid.NewGuid().ToString("N"), conversation, now, now),
            (_, existing) => existing with
            {
                Conversation = conversation,
                LastActivityAtUtc = now
            });

        return Task.FromResult(session);
    }
}
