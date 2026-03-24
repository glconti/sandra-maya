using System.Globalization;
using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Telegram;

public sealed class TelegramOutboundMessageDispatcher : IOutboundMessageDispatcher
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;

    public TelegramOutboundMessageDispatcher(ITelegramBotApiClient telegramBotApiClient)
    {
        _telegramBotApiClient = telegramBotApiClient;
    }

    public Task DispatchAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.Conversation.Platform, TransportPlatforms.Telegram, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported outbound platform '{message.Conversation.Platform}'.");
        }

        if (!long.TryParse(message.Conversation.ConversationId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
        {
            throw new InvalidOperationException(
                $"Conversation id '{message.Conversation.ConversationId}' is not a valid Telegram chat id.");
        }

        return _telegramBotApiClient.SendMessageAsync(chatId, message.Text, cancellationToken);
    }
}
