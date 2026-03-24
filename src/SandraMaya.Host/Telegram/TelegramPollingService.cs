using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Telegram;

public sealed class TelegramPollingService : BackgroundService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramUpdateRouter _updateRouter;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramPollingService> _logger;
    private long? _offset;

    public TelegramPollingService(
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramUpdateRouter updateRouter,
        IOptions<TelegramOptions> options,
        ILogger<TelegramPollingService> logger)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _updateRouter = updateRouter;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var allowedUpdates = _options.IncludeEditedMessages
            ? new[] { "message", "edited_message" }
            : new[] { "message" };

        _logger.LogInformation("Telegram polling service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _telegramBotApiClient.GetUpdatesAsync(
                    _offset,
                    _options.MaxUpdatesPerRequest,
                    _options.PollingTimeoutSeconds,
                    allowedUpdates,
                    stoppingToken);

                foreach (var update in updates.OrderBy(candidate => candidate.UpdateId))
                {
                    try
                    {
                        await _updateRouter.RouteAsync(update, stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to process Telegram update {UpdateId}.", update.UpdateId);
                    }
                    finally
                    {
                        _offset = update.UpdateId + 1;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Telegram polling loop failed. Retrying after backoff.");
                await Task.Delay(TimeSpan.FromSeconds(_options.ErrorBackoffSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("Telegram polling service stopped.");
    }
}
