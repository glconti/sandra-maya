using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Telegram;

public interface ITelegramBotApiClient
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long? offset,
        int limit,
        int timeoutSeconds,
        IReadOnlyList<string> allowedUpdates,
        CancellationToken cancellationToken);

    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken);
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
    InboundMessage? Map(TelegramUpdate update);
}
