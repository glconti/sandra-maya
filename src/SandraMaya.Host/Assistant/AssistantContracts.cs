using GitHub.Copilot.SDK;

namespace SandraMaya.Host.Assistant;

public static class TransportPlatforms
{
    public const string Telegram = "telegram";
}

public enum InboundAttachmentKind
{
    Unknown = 0,
    Document = 1,
    Photo = 2,
    Audio = 3,
    Video = 4,
    Voice = 5
}

public sealed record ConversationReference(
    string Platform,
    string ConversationId,
    string UserId);

public static class ConversationReferenceExtensions
{
    public static string ToKey(this ConversationReference conversation) =>
        $"{conversation.Platform}:{conversation.ConversationId}";
}

public sealed record UserReference(
    string Id,
    string? Username,
    string? DisplayName);

public sealed record InboundAttachment(
    InboundAttachmentKind Kind,
    string AttachmentId,
    string? FileName,
    string? ContentType,
    long? SizeBytes)
{
    /// <summary>Raw bytes of the downloaded file, if download succeeded.</summary>
    public byte[]? Content { get; init; }
}

public sealed record InboundMessage(
    string MessageId,
    ConversationReference Conversation,
    UserReference Sender,
    string? Text,
    string? Caption,
    IReadOnlyList<InboundAttachment> Attachments,
    DateTimeOffset ReceivedAtUtc)
{
    public string? EffectiveText => string.IsNullOrWhiteSpace(Text) ? Caption : Text;
}

public sealed record AssistantReply(string Text);

public sealed record AssistantTurnResult(
    string SessionId,
    IReadOnlyList<AssistantReply> Replies);

public sealed record AssistantSession(
    string SessionId,
    ConversationReference Conversation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc);

public sealed record OutboundMessage(
    ConversationReference Conversation,
    string Text);

public interface IAssistantOrchestrator
{
    Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken);
}

public interface IAssistantSessionStore
{
    Task<AssistantSession> GetOrCreateAsync(ConversationReference conversation, CancellationToken cancellationToken);
}

public interface IInboundMessageRouter
{
    Task RouteAsync(InboundMessage message, CancellationToken cancellationToken);
}

public interface IOutboundMessageDispatcher
{
    Task DispatchAsync(OutboundMessage message, CancellationToken cancellationToken);
}

public interface IActiveAssistantTurnRegistry
{
    IDisposable Track(ConversationReference conversation, CopilotSession session);

    Task<bool> RequestStopAsync(ConversationReference conversation, CancellationToken cancellationToken);

    bool TryConsumeStopRequest(ConversationReference conversation);
}
