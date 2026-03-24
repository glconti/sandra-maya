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
        var inboundMessage = await _messageMapper.MapAsync(update, cancellationToken);

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
    private readonly ITelegramBotApiClient _apiClient;
    private readonly ILogger<TelegramMessageMapper> _logger;

    public TelegramMessageMapper(
        ITelegramBotApiClient apiClient,
        ILogger<TelegramMessageMapper> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<InboundMessage?> MapAsync(TelegramUpdate update, CancellationToken cancellationToken)
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

        var attachments = await BuildAttachmentsAsync(message, cancellationToken);

        return new InboundMessage(
            message.MessageId.ToString(CultureInfo.InvariantCulture),
            conversation,
            sender,
            message.Text,
            message.Caption,
            attachments,
            receivedAtUtc);
    }

    private async Task<IReadOnlyList<InboundAttachment>> BuildAttachmentsAsync(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        var attachments = new List<InboundAttachment>();

        if (message.Document is not null)
        {
            attachments.Add(await DownloadAttachmentAsync(
                new InboundAttachment(
                    InboundAttachmentKind.Document,
                    message.Document.FileUniqueId ?? message.Document.FileId ?? Guid.NewGuid().ToString("N"),
                    message.Document.FileName,
                    message.Document.MimeType,
                    message.Document.FileSize),
                message.Document.FileId,
                cancellationToken));
        }

        if (message.Photo is not null)
        {
            // Take the highest-resolution photo (last in the array)
            var photo = message.Photo[^1];
            attachments.Add(await DownloadAttachmentAsync(
                new InboundAttachment(
                    InboundAttachmentKind.Photo,
                    photo.FileUniqueId ?? photo.FileId ?? Guid.NewGuid().ToString("N"),
                    null,
                    "image/jpeg",
                    photo.FileSize),
                photo.FileId,
                cancellationToken));
        }

        if (message.Audio is not null)
        {
            attachments.Add(await DownloadAttachmentAsync(
                new InboundAttachment(
                    InboundAttachmentKind.Audio,
                    message.Audio.FileUniqueId ?? message.Audio.FileId ?? Guid.NewGuid().ToString("N"),
                    message.Audio.FileName,
                    message.Audio.MimeType,
                    message.Audio.FileSize),
                message.Audio.FileId,
                cancellationToken));
        }

        if (message.Video is not null)
        {
            attachments.Add(await DownloadAttachmentAsync(
                new InboundAttachment(
                    InboundAttachmentKind.Video,
                    message.Video.FileUniqueId ?? message.Video.FileId ?? Guid.NewGuid().ToString("N"),
                    message.Video.FileName,
                    message.Video.MimeType,
                    message.Video.FileSize),
                message.Video.FileId,
                cancellationToken));
        }

        if (message.Voice is not null)
        {
            attachments.Add(await DownloadAttachmentAsync(
                new InboundAttachment(
                    InboundAttachmentKind.Voice,
                    message.Voice.FileUniqueId ?? message.Voice.FileId ?? Guid.NewGuid().ToString("N"),
                    null,
                    message.Voice.MimeType,
                    message.Voice.FileSize),
                message.Voice.FileId,
                cancellationToken));
        }

        return attachments;
    }

    private async Task<InboundAttachment> DownloadAttachmentAsync(
        InboundAttachment attachment,
        string? fileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return attachment;
        }

        try
        {
            var fileInfo = await _apiClient.GetFileAsync(fileId, cancellationToken);

            if (string.IsNullOrWhiteSpace(fileInfo?.FilePath))
            {
                _logger.LogWarning("Telegram getFile returned no file_path for file_id {FileId}.", fileId);
                return attachment;
            }

            var content = await _apiClient.DownloadFileAsync(fileInfo.FilePath, cancellationToken);
            return attachment with { Content = content };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download Telegram file {FileId}; attachment will have no content.", fileId);
            return attachment;
        }
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
