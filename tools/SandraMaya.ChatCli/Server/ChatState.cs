using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using SandraMaya.ChatCli.Text;

namespace SandraMaya.ChatCli.Server;

/// <summary>
/// Singleton in-memory state shared between the Telegram mock API endpoints
/// and the CLI management endpoints.
/// </summary>
public sealed class ChatState
{
    private readonly Channel<PendingUpdate> _inboundQueue =
        Channel.CreateUnbounded<PendingUpdate>(new UnboundedChannelOptions { SingleReader = false });

    private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> _pendingReplies = new();

    private readonly List<ConversationEntry> _history = [];
    private readonly Lock _historyLock = new();

    private long _nextUpdateId = 1000;
    private long _nextMessageId = 1;

    public long NextUpdateId() => Interlocked.Increment(ref _nextUpdateId);
    public long NextMessageId() => Interlocked.Increment(ref _nextMessageId);

    // --- Inbound queue (consumed by getUpdates handler) ---

    public ValueTask EnqueueUpdateAsync(PendingUpdate update, CancellationToken ct = default) =>
        _inboundQueue.Writer.WriteAsync(update, ct);

    public ChannelReader<PendingUpdate> InboundReader => _inboundQueue.Reader;

    // --- Pending reply tracking ---

    public TaskCompletionSource<string> RegisterPendingReply(long chatId)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[chatId] = tcs;
        return tcs;
    }

    public void CompleteReply(long chatId, string replyText)
    {
        replyText = TextSanitizer.StripControlCharacters(replyText);

        if (_pendingReplies.TryRemove(chatId, out var tcs))
            tcs.TrySetResult(replyText);
    }

    // --- Conversation history ---

    public void AppendHistory(string role, string text)
    {
        text = TextSanitizer.StripControlCharacters(text);

        lock (_historyLock)
            _history.Add(new ConversationEntry(role, text, DateTimeOffset.UtcNow));
    }

    public IReadOnlyList<ConversationEntry> GetHistory()
    {
        lock (_historyLock)
            return [.. _history];
    }
}

public sealed record PendingUpdate(
    long UpdateId,
    long MessageId,
    long ChatId,
    long UserId,
    string Username,
    string? Text);

public sealed record ConversationEntry(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("at")] DateTimeOffset At);
