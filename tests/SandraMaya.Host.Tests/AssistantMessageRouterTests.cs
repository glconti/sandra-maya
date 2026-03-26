using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Tests;

public sealed class AssistantMessageRouterTests
{
    [Fact]
    public async Task RouteAsync_SendsTypingAndDispatchesCompletedReply_ForNormalMessage()
    {
        var dispatcher = new RecordingOutboundDispatcher();
        var telegramClient = new RecordingTelegramClient();
        var orchestrator = new DelayedReplyOrchestrator();
        var provider = new ServiceCollection()
            .AddSingleton<IAssistantOrchestrator>(orchestrator)
            .BuildServiceProvider();

        var subject = new AssistantMessageRouter(
            new SimpleScopeFactory(provider),
            dispatcher,
            new NoOpTurnRegistry(),
            telegramClient,
            new TestHostApplicationLifetime(),
            NullLogger<AssistantMessageRouter>.Instance);

        await subject.RouteAsync(
            new InboundMessage(
                "message-1",
                new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1"),
                new UserReference("user-1", "agent", "Agent"),
                "hello",
                null,
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        await dispatcher.WaitForDispatchAsync(TimeSpan.FromSeconds(5));

        Assert.Contains(TelegramChatActions.Typing, telegramClient.Actions);
        Assert.Single(dispatcher.Messages);
        Assert.Equal("completed reply", dispatcher.Messages[0].Text);
    }

    [Fact]
    public async Task RouteAsync_StopMessage_RequestsStopAndDoesNotDispatchReply()
    {
        var dispatcher = new RecordingOutboundDispatcher();
        var telegramClient = new RecordingTelegramClient();
        var turnRegistry = new RecordingTurnRegistry();
        var provider = new ServiceCollection().BuildServiceProvider();

        var subject = new AssistantMessageRouter(
            new SimpleScopeFactory(provider),
            dispatcher,
            turnRegistry,
            telegramClient,
            new TestHostApplicationLifetime(),
            NullLogger<AssistantMessageRouter>.Instance);

        await subject.RouteAsync(
            new InboundMessage(
                "message-2",
                new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1"),
                new UserReference("user-1", "agent", "Agent"),
                "stop",
                null,
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.True(turnRegistry.StopRequested);
        Assert.Empty(dispatcher.Messages);
        Assert.Empty(telegramClient.Actions);
    }

    [Fact]
    public async Task RouteAsync_MessageContainingStopButNotCommand_IsProcessedNormally()
    {
        var dispatcher = new RecordingOutboundDispatcher();
        var telegramClient = new RecordingTelegramClient();
        var turnRegistry = new RecordingTurnRegistry();
        var orchestrator = new DelayedReplyOrchestrator();
        var provider = new ServiceCollection()
            .AddSingleton<IAssistantOrchestrator>(orchestrator)
            .BuildServiceProvider();

        var subject = new AssistantMessageRouter(
            new SimpleScopeFactory(provider),
            dispatcher,
            turnRegistry,
            telegramClient,
            new TestHostApplicationLifetime(),
            NullLogger<AssistantMessageRouter>.Instance);

        await subject.RouteAsync(
            new InboundMessage(
                "message-3",
                new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1"),
                new UserReference("user-1", "agent", "Agent"),
                "please stop searching and summarize",
                null,
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        await dispatcher.WaitForDispatchAsync(TimeSpan.FromSeconds(5));

        Assert.False(turnRegistry.StopRequested);
        Assert.Single(dispatcher.Messages);
        Assert.Equal("completed reply", dispatcher.Messages[0].Text);
    }

    [Fact]
    public async Task RouteAsync_ShutdownCancelsInFlightProcessing_AndDoesNotDispatchReply()
    {
        var dispatcher = new RecordingOutboundDispatcher();
        var telegramClient = new RecordingTelegramClient();
        var orchestrator = new CancellationAwareOrchestrator();
        var lifetime = new TestHostApplicationLifetime();
        var provider = new ServiceCollection()
            .AddSingleton<IAssistantOrchestrator>(orchestrator)
            .BuildServiceProvider();

        var subject = new AssistantMessageRouter(
            new SimpleScopeFactory(provider),
            dispatcher,
            new NoOpTurnRegistry(),
            telegramClient,
            lifetime,
            NullLogger<AssistantMessageRouter>.Instance);

        await subject.RouteAsync(
            new InboundMessage(
                "message-4",
                new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1"),
                new UserReference("user-1", "agent", "Agent"),
                "hello",
                null,
                [],
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        await orchestrator.WaitForStartAsync(TimeSpan.FromSeconds(5));
        lifetime.StopApplication();
        await orchestrator.WaitForCancellationAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(100);

        Assert.Empty(dispatcher.Messages);
    }

    private sealed class DelayedReplyOrchestrator : IAssistantOrchestrator
    {
        public async Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            return new AssistantTurnResult(
                "session-1",
                [new AssistantReply("completed reply")]);
        }
    }

    private sealed class CancellationAwareOrchestrator : IAssistantOrchestrator
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken)
        {
            _started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _cancelled.TrySetResult();
                throw;
            }

            throw new InvalidOperationException("ProcessAsync should have been cancelled.");
        }

        public Task WaitForStartAsync(TimeSpan timeout) => _started.Task.WaitAsync(timeout);

        public Task WaitForCancellationAsync(TimeSpan timeout) => _cancelled.Task.WaitAsync(timeout);
    }

    private sealed class RecordingOutboundDispatcher : IOutboundMessageDispatcher
    {
        private readonly TaskCompletionSource _dispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<OutboundMessage> Messages { get; } = [];

        public Task DispatchAsync(OutboundMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            _dispatch.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForDispatchAsync(TimeSpan timeout) =>
            _dispatch.Task.WaitAsync(timeout);
    }

    private sealed class RecordingTelegramClient : ITelegramBotApiClient
    {
        public List<string> Actions { get; } = [];

        public Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long? offset, int limit, int timeoutSeconds, IReadOnlyList<string> allowedUpdates, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TelegramUpdate>>([]);

        public Task SendMessageAsync(long chatId, string text, string? parseMode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendChatActionAsync(long chatId, string action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }

        public Task<TelegramFile?> GetFileAsync(string fileId, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramFile?>(null);

        public Task<byte[]> DownloadFileAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());
    }

    private sealed class RecordingTurnRegistry : IActiveAssistantTurnRegistry
    {
        public bool StopRequested { get; private set; }

        public IDisposable Track(ConversationReference conversation, GitHub.Copilot.SDK.CopilotSession session) =>
            new NoopDisposable();

        public Task<bool> RequestStopAsync(ConversationReference conversation, CancellationToken cancellationToken)
        {
            StopRequested = true;
            return Task.FromResult(true);
        }

        public bool TryConsumeStopRequest(ConversationReference conversation)
        {
            var wasRequested = StopRequested;
            StopRequested = false;
            return wasRequested;
        }
    }

    private sealed class NoOpTurnRegistry : IActiveAssistantTurnRegistry
    {
        public IDisposable Track(ConversationReference conversation, GitHub.Copilot.SDK.CopilotSession session) =>
            new NoopDisposable();

        public Task<bool> RequestStopAsync(ConversationReference conversation, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public bool TryConsumeStopRequest(ConversationReference conversation) => false;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class SimpleScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SimpleScope(serviceProvider);
    }

    private sealed class SimpleScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public void Dispose()
        {
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }
}
