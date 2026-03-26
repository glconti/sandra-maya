using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Assistant;

public sealed class AssistantMessageRouter : IInboundMessageRouter
{
    private static readonly TimeSpan TelegramTypingRefreshInterval = TimeSpan.FromSeconds(4);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboundMessageDispatcher _outboundMessageDispatcher;
    private readonly IActiveAssistantTurnRegistry _turnRegistry;
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<AssistantMessageRouter> _logger;
    private readonly ConcurrentDictionary<string, ConversationMailbox> _mailboxes = new(StringComparer.OrdinalIgnoreCase);

    public AssistantMessageRouter(
        IServiceScopeFactory scopeFactory,
        IOutboundMessageDispatcher outboundMessageDispatcher,
        IActiveAssistantTurnRegistry turnRegistry,
        ITelegramBotApiClient telegramBotApiClient,
        IHostApplicationLifetime appLifetime,
        ILogger<AssistantMessageRouter> logger)
    {
        _scopeFactory = scopeFactory;
        _outboundMessageDispatcher = outboundMessageDispatcher;
        _turnRegistry = turnRegistry;
        _telegramBotApiClient = telegramBotApiClient;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    public async Task RouteAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        if (IsStopRequest(message))
        {
            var stopped = await _turnRegistry.RequestStopAsync(message.Conversation, cancellationToken);
            if (!stopped)
            {
                _logger.LogDebug(
                    "Received stop request for conversation {ConversationId}, but no active turn was running.",
                    message.Conversation.ConversationId);
            }

            return;
        }

        var mailbox = _mailboxes.GetOrAdd(message.Conversation.ToKey(), _ => new ConversationMailbox());
        await mailbox.Writer.WriteAsync(message, cancellationToken);
        StartWorkerIfNeeded(mailbox);
    }

    private void StartWorkerIfNeeded(ConversationMailbox mailbox)
    {
        if (Interlocked.CompareExchange(ref mailbox.WorkerStarted, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() => ProcessMailboxAsync(mailbox, _appLifetime.ApplicationStopping), CancellationToken.None);
    }

    private async Task ProcessMailboxAsync(ConversationMailbox mailbox, CancellationToken stoppingToken)
    {
        try
        {
            while (await mailbox.Reader.WaitToReadAsync(stoppingToken))
            {
                while (mailbox.Reader.TryRead(out var message))
                {
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to process assistant message {MessageId} for conversation {ConversationId}.",
                            message.MessageId,
                            message.Conversation.ConversationId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref mailbox.WorkerStarted, 0);
        }
    }

    private async Task ProcessMessageAsync(InboundMessage message, CancellationToken stoppingToken)
    {
        if (!TryParseTelegramChatId(message.Conversation.ConversationId, out var chatId))
        {
            _logger.LogWarning(
                "Conversation id '{ConversationId}' is not a valid Telegram chat id.",
                message.Conversation.ConversationId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAssistantOrchestrator>();

        using var typingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var typingTask = RunTypingLoopAsync(chatId, typingCts.Token);

        try
        {
            var result = await orchestrator.ProcessAsync(message, stoppingToken);

            if (result.Replies.Count == 0)
            {
                _logger.LogDebug(
                    "Assistant session {SessionId} completed without replies for conversation {ConversationId}.",
                    result.SessionId,
                    message.Conversation.ConversationId);
                return;
            }

            foreach (var reply in result.Replies)
            {
                await _outboundMessageDispatcher.DispatchAsync(
                    new OutboundMessage(message.Conversation, reply.Text),
                    stoppingToken);
            }
        }
        finally
        {
            typingCts.Cancel();

            try
            {
                await typingTask;
            }
            catch (OperationCanceledException) when (typingCts.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunTypingLoopAsync(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await _telegramBotApiClient.SendChatActionAsync(chatId, TelegramChatActions.Typing, cancellationToken);

            using var timer = new PeriodicTimer(TelegramTypingRefreshInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await _telegramBotApiClient.SendChatActionAsync(chatId, TelegramChatActions.Typing, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram typing indicator failed for chat {ChatId}.", chatId);
        }
    }

    private static bool IsStopRequest(InboundMessage message)
    {
        var text = message.EffectiveText?.Trim();
        return string.Equals(text, "stop", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "/stop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseTelegramChatId(string conversationId, out long chatId) =>
        long.TryParse(conversationId, out chatId);

    private sealed class ConversationMailbox
    {
        public ConversationMailbox()
        {
            var options = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

            MailboxChannel = System.Threading.Channels.Channel.CreateUnbounded<InboundMessage>(options);
        }

        public Channel<InboundMessage> MailboxChannel { get; }

        public ChannelReader<InboundMessage> Reader => MailboxChannel.Reader;

        public ChannelWriter<InboundMessage> Writer => MailboxChannel.Writer;

        public int WorkerStarted;
    }
}
