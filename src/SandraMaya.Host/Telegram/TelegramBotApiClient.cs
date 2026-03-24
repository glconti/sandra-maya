using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Telegram;

public sealed class TelegramBotApiClient : ITelegramBotApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<TelegramOptions> _options;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public TelegramBotApiClient(HttpClient httpClient, IOptions<TelegramOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long? offset,
        int limit,
        int timeoutSeconds,
        IReadOnlyList<string> allowedUpdates,
        CancellationToken cancellationToken)
    {
        var request = new TelegramGetUpdatesRequest
        {
            Offset = offset,
            Limit = limit,
            Timeout = timeoutSeconds,
            AllowedUpdates = allowedUpdates
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"bot{_options.Value.BotToken}/getUpdates",
            request,
            _jsonOptions,
            cancellationToken);

        return await ReadResponseAsync<List<TelegramUpdate>>(response, cancellationToken) ?? [];
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var request = new TelegramSendMessageRequest
        {
            ChatId = chatId,
            Text = text
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"bot{_options.Value.BotToken}/sendMessage",
            request,
            _jsonOptions,
            cancellationToken);

        _ = await ReadResponseAsync<TelegramMessage>(response, cancellationToken);
    }

    private async Task<T?> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Telegram API returned HTTP {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var envelope = JsonSerializer.Deserialize<TelegramApiResponse<T>>(body, _jsonOptions)
                       ?? throw new InvalidOperationException("Telegram API returned an unreadable response.");

        if (!envelope.Ok)
        {
            throw new InvalidOperationException(
                $"Telegram API reported an error: {envelope.Description ?? "Unknown error"}");
        }

        return envelope.Result;
    }
}
