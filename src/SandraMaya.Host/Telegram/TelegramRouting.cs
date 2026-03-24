using System.Globalization;
using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Telegram;

public sealed class TelegramUpdateRouter : ITelegramUpdateRouter
{
    private readonly IReadOnlyList<ITelegramUpdateHandler> _handlers;
    private readonly ILogger<TelegramUpdateRouter> _logger;

    public TelegramUpdateRouter(
        IEnumerable<ITelegramUpdateHandler> handlers,
        ILogger<TelegramUpdateRouter> logger)
    {
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public async Task RouteAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(candidate => candidate.CanHandle(update));

        if (handler is null)
        {
            _logger.LogDebug("Skipping unsupported Telegram update {UpdateId}.", update.UpdateId);
            return;
        }

        await handler.HandleAsync(update, cancellationToken);
    }
}

public sealed class TelegramMessageUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramMessageMapper _messageMapper;
    private readonly IInboundMessageRouter _messageRouter;
    private readonly ILogger<TelegramMessageUpdateHandler> _logger;

    public TelegramMessageUpdateHandler(
        ITelegramMessageMapper messageMapper,
        IInboundMessageRouter messageRouter,
        ILogger<TelegramMessageUpdateHandler> logger)
    {
        _messageMapper = messageMapper;
        _messageRouter = messageRouter;
        _logger = logger;
    }

    public bool CanHandle(TelegramUpdate update) =>
        update.Message is not null || update.EditedMessage is not null;

    public async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var inboundMessage = _messageMapper.Map(update);

        if (inboundMessage is null)
        {
            _logger.LogWarning("Telegram update {UpdateId} could not be mapped to an inbound message.", update.UpdateId);
            return;
        }

        await _messageRouter.RouteAsync(inboundMessage, cancellationToken);
    }
}

public sealed class TelegramMessageMapper : ITelegramMessageMapper
{
    public InboundMessage? Map(TelegramUpdate update)
    {
        var message = update.Message ?? update.EditedMessage;

        if (message?.Chat is null)
        {
            return null;
        }

        var senderId = message.From?.Id ?? message.Chat.Id;
        var sender = new UserReference(
            senderId.ToString(CultureInfo.InvariantCulture),
            message.From?.Username,
            BuildDisplayName(message.From, message.Chat));

        var conversation = new ConversationReference(
            TransportPlatforms.Telegram,
            message.Chat.Id.ToString(CultureInfo.InvariantCulture),
            sender.Id);

        var receivedAtUtc = message.Date > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.Date)
            : DateTimeOffset.UtcNow;

        return new InboundMessage(
            message.MessageId.ToString(CultureInfo.InvariantCulture),
            conversation,
            sender,
            message.Text,
            message.Caption,
            BuildAttachments(message),
            receivedAtUtc);
    }

    private static IReadOnlyList<InboundAttachment> BuildAttachments(TelegramMessage message)
    {
        var attachments = new List<InboundAttachment>();

        if (message.Document is not null)
        {
            attachments.Add(new InboundAttachment(
                InboundAttachmentKind.Document,
                message.Document.FileUniqueId ?? message.Document.FileId ?? Guid.NewGuid().ToString("N"),
                message.Document.FileName,
                message.Document.MimeType,
                message.Document.FileSize));
        }

        if (message.Photo is not null)
        {
            attachments.AddRange(message.Photo.Select(photo => new InboundAttachment(
                InboundAttachmentKind.Photo,
                photo.FileUniqueId ?? photo.FileId ?? Guid.NewGuid().ToString("N"),
                null,
                "image/jpeg",
                photo.FileSize)));
        }

        if (message.Audio is not null)
        {
            attachments.Add(new InboundAttachment(
                InboundAttachmentKind.Audio,
                message.Audio.FileUniqueId ?? message.Audio.FileId ?? Guid.NewGuid().ToString("N"),
                message.Audio.FileName,
                message.Audio.MimeType,
                message.Audio.FileSize));
        }

        if (message.Video is not null)
        {
            attachments.Add(new InboundAttachment(
                InboundAttachmentKind.Video,
                message.Video.FileUniqueId ?? message.Video.FileId ?? Guid.NewGuid().ToString("N"),
                message.Video.FileName,
                message.Video.MimeType,
                message.Video.FileSize));
        }

        if (message.Voice is not null)
        {
            attachments.Add(new InboundAttachment(
                InboundAttachmentKind.Voice,
                message.Voice.FileUniqueId ?? message.Voice.FileId ?? Guid.NewGuid().ToString("N"),
                null,
                message.Voice.MimeType,
                message.Voice.FileSize));
        }

        return attachments;
    }

    private static string? BuildDisplayName(TelegramUser? user, TelegramChat chat)
    {
        if (user is null)
        {
            return chat.Title;
        }

        var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(fullName) ? user.Username : fullName;
    }
}
