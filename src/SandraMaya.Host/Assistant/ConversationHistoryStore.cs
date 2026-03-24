using System.Collections.Concurrent;
using OpenAI.Chat;

namespace SandraMaya.Host.Assistant;

/// <summary>
/// Thread-safe singleton store for conversation histories.
/// Extracted from the orchestrator so it can survive across scoped lifetimes.
/// </summary>
public sealed class ConversationHistoryStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _histories = new();

    public List<ChatMessage> GetOrCreate(string sessionId) =>
        _histories.GetOrAdd(sessionId, _ => new List<ChatMessage>());

    public bool TryRemove(string sessionId) =>
        _histories.TryRemove(sessionId, out _);
}
