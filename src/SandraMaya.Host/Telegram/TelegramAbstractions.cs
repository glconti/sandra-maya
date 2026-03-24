using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Telegram;

public interface ITelegramBotApiClient
{
    Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken);

    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long? offset,
        int limit,
        int timeoutSeconds,
        IReadOnlyList<string> allowedUpdates,
        CancellationToken cancellationToken);

    Task SendMessageAsync(long chatId, string text, string? parseMode, CancellationToken cancellationToken);

    Task<TelegramFile?> GetFileAsync(string fileId, CancellationToken cancellationToken);

    Task<byte[]> DownloadFileAsync(string filePath, CancellationToken cancellationToken);
}

public interface ITelegramUpdateRouter
{
    Task RouteAsync(TelegramUpdate update, CancellationToken cancellationToken);
}

public interface ITelegramUpdateHandler
{
    bool CanHandle(TelegramUpdate update);

    Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken);
}

public interface ITelegramMessageMapper
{
    Task<InboundMessage?> MapAsync(TelegramUpdate update, CancellationToken cancellationToken);
}
