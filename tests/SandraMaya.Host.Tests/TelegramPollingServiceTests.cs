using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Tests;

public sealed class TelegramPollingServiceTests
{
    [Fact]
    public async Task StartAsync_ClearsWebhookBeforePollingForUpdates()
    {
        var apiClient = new RecordingTelegramBotApiClient();
        var router = new RecordingTelegramUpdateRouter();
        var options = Options.Create(new TelegramOptions
        {
            BotToken = "test-token",
            PollingTimeoutSeconds = 1,
            ErrorBackoffSeconds = 1,
            MaxUpdatesPerRequest = 10,
            IncludeEditedMessages = true
        });

        using var service = new TelegramPollingService(
            apiClient,
            router,
            options,
            NullLogger<TelegramPollingService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await router.WaitForUpdateAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(["deleteWebhook", "getUpdates"], apiClient.CallSequence.Take(2));
        Assert.False(apiClient.DropPendingUpdates);
        Assert.Single(router.Updates);
    }

    private sealed class RecordingTelegramBotApiClient : ITelegramBotApiClient
    {
        private int _getUpdatesCalls;

        public List<string> CallSequence { get; } = [];

        public bool DropPendingUpdates { get; private set; }

        public Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken)
        {
            CallSequence.Add("deleteWebhook");
            DropPendingUpdates = dropPendingUpdates;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
            long? offset,
            int limit,
            int timeoutSeconds,
            IReadOnlyList<string> allowedUpdates,
            CancellationToken cancellationToken)
        {
            CallSequence.Add("getUpdates");

            if (Interlocked.Increment(ref _getUpdatesCalls) == 1)
            {
                return Task.FromResult<IReadOnlyList<TelegramUpdate>>(
                [
                    new TelegramUpdate
                    {
                        UpdateId = 1001,
                        Message = new TelegramMessage
                        {
                            MessageId = 42,
                            Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Text = "hello",
                            Chat = new TelegramChat
                            {
                                Id = 999
                            }
                        }
                    }
                ]);
            }

            return WaitForCancellationAsync(cancellationToken);
        }

        public Task SendMessageAsync(long chatId, string text, string? parseMode, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendChatActionAsync(long chatId, string action, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TelegramFile?> GetFileAsync(string fileId, CancellationToken cancellationToken) =>
            Task.FromResult<TelegramFile?>(null);

        public Task<byte[]> DownloadFileAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());

        private static async Task<IReadOnlyList<TelegramUpdate>> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            return [];
        }
    }

    private sealed class RecordingTelegramUpdateRouter : ITelegramUpdateRouter
    {
        private readonly TaskCompletionSource _processed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<TelegramUpdate> Updates { get; } = [];

        public Task RouteAsync(TelegramUpdate update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            _processed.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task WaitForUpdateAsync(TimeSpan timeout)
        {
            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await _processed.Task.WaitAsync(cancellationTokenSource.Token);
        }
    }
}
