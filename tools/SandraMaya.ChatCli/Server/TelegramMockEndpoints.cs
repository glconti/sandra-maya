using System.Text.Json;
using System.Text.Json.Nodes;
using SandraMaya.ChatCli.Text;

namespace SandraMaya.ChatCli.Server;

/// <summary>
/// Minimal ASP.NET Core route handlers that simulate the Telegram Bot API.
/// The Sandra Maya Host polls these endpoints just as it would the real Telegram service.
/// </summary>
public static class TelegramMockEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        // The bot token is part of the URL path, e.g. /bot{token}/getUpdates
        app.MapPost("/bot{token}/deleteWebhook", DeleteWebhook);
        app.MapPost("/bot{token}/getUpdates", GetUpdates);
        app.MapPost("/bot{token}/sendMessage", SendMessage);
        app.MapPost("/bot{token}/getFile", GetFile);
        app.MapGet("/file/bot{token}/{**filePath}", DownloadFile);
    }

    private static IResult DeleteWebhook(string token) =>
        Results.Ok(Envelope(true));

    private static async Task<IResult> GetUpdates(
        string token,
        HttpRequest request,
        ChatState state,
        CancellationToken ct)
    {
        long? offset = null;
        int timeoutSeconds = 1;

        try
        {
            var body = await new StreamReader(request.Body).ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var doc = JsonNode.Parse(body);
                offset = doc?["offset"]?.GetValue<long?>();
                timeoutSeconds = doc?["timeout"]?.GetValue<int>() ?? 1;
            }
        }
        catch (JsonException) { /* use defaults */ }

        var updates = new List<object>();

        // Drain immediately available updates
        while (state.InboundReader.TryRead(out var pending))
        {
            if (offset is null || pending.UpdateId >= offset)
                updates.Add(BuildUpdateObject(pending));
        }

        if (updates.Count == 0)
        {
            // Long-poll: wait for first update or timeout
            using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pollCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

            try
            {
                var first = await state.InboundReader.ReadAsync(pollCts.Token);
                if (offset is null || first.UpdateId >= offset)
                    updates.Add(BuildUpdateObject(first));

                while (state.InboundReader.TryRead(out var more))
                {
                    if (offset is null || more.UpdateId >= offset)
                        updates.Add(BuildUpdateObject(more));
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout — return empty list (expected Telegram behaviour)
            }
        }

        return Results.Ok(Envelope(updates));
    }

    private static async Task<IResult> SendMessage(
        string token,
        HttpRequest request,
        ChatState state,
        CancellationToken ct)
    {
        long chatId = 0;
        string text = string.Empty;
        string? parseMode = null;

        try
        {
            var body = await new StreamReader(request.Body).ReadToEndAsync(ct);
            var doc = JsonNode.Parse(body);
            chatId = doc?["chat_id"]?.GetValue<long>() ?? 0;
            text = doc?["text"]?.GetValue<string>() ?? string.Empty;
            parseMode = doc?["parse_mode"]?.GetValue<string>();
        }
        catch (JsonException) { /* use defaults */ }

        text = TextSanitizer.StripControlCharacters(text);

        state.CompleteReply(chatId, text);
        state.AppendHistory("bot", text);

        var sentMessage = new
        {
            message_id = state.NextMessageId(),
            date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            chat = new { id = chatId },
            text
        };

        return Results.Ok(Envelope(sentMessage));
    }

    private static IResult GetFile(string token) =>
        // The CLI does not register files; return a not-found API error
        Results.BadRequest(new { ok = false, description = "File not found" });

    private static IResult DownloadFile(string token, string filePath) =>
        Results.NotFound();

    private static object BuildUpdateObject(PendingUpdate p) => new
    {
        update_id = p.UpdateId,
        message = new
        {
            message_id = p.MessageId,
            date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            chat = new { id = p.ChatId, type = "private" },
            from = new
            {
                id = p.UserId,
                first_name = p.Username,
                username = p.Username
            },
            text = p.Text
        }
    };

    private static object Envelope<T>(T result) => new { ok = true, result };
}
