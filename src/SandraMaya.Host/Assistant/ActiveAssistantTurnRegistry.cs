using System.Collections.Concurrent;
using GitHub.Copilot.SDK;

namespace SandraMaya.Host.Assistant;

public sealed class InMemoryActiveAssistantTurnRegistry : IActiveAssistantTurnRegistry
{
    private readonly ConcurrentDictionary<string, TurnControl> _turns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _stopRequests = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Track(ConversationReference conversation, CopilotSession session)
    {
        var key = conversation.ToKey();
        var control = new TurnControl(session);
        _stopRequests.TryRemove(key, out _);

        if (!_turns.TryAdd(key, control))
        {
            throw new InvalidOperationException($"Conversation '{key}' already has an active turn.");
        }

        return new ReleaseHandle(() => _turns.TryRemove(key, out _));
    }

    public async Task<bool> RequestStopAsync(ConversationReference conversation, CancellationToken cancellationToken)
    {
        var key = conversation.ToKey();

        if (!_turns.TryGetValue(key, out var control))
        {
            return false;
        }

        _stopRequests[key] = 0;

        try
        {
            await control.Session.AbortAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            _stopRequests.TryRemove(key, out _);
            return false;
        }
    }

    public bool TryConsumeStopRequest(ConversationReference conversation)
    {
        var key = conversation.ToKey();
        return _stopRequests.TryRemove(key, out _);
    }

    private sealed class TurnControl(CopilotSession session)
    {
        public CopilotSession Session { get; } = session;
    }

    private sealed class ReleaseHandle(Action release) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                release();
            }
        }
    }
}
