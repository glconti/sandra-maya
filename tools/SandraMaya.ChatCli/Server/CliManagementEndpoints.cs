using System.Text.Json;
using System.Text.Json.Nodes;
using SandraMaya.ChatCli.Text;

namespace SandraMaya.ChatCli.Server;

/// <summary>
/// Management endpoints used by the one-shot CLI commands (send, status, history, shutdown).
/// </summary>
public static class CliManagementEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static void Map(WebApplication app)
    {
        app.MapPost("/cli/send", SendMessage);
        app.MapGet("/cli/status", GetStatus);
        app.MapGet("/cli/history", GetHistory);
        app.MapPost("/cli/shutdown", Shutdown);
    }

    private static async Task<IResult> SendMessage(
        HttpRequest request,
        ChatState state,
        CancellationToken ct)
    {
        string? text = null;
        long chatId = 999;
        long userId = 1;
        string username = "agent";
        int timeoutSeconds = 30;

        try
        {
            var body = await new StreamReader(request.Body).ReadToEndAsync(ct);
            var doc = JsonNode.Parse(body);
            text = doc?["text"]?.GetValue<string>();
            chatId = doc?["chatId"]?.GetValue<long>() ?? chatId;
            userId = doc?["userId"]?.GetValue<long>() ?? userId;
            username = doc?["username"]?.GetValue<string>() ?? username;
            timeoutSeconds = doc?["timeoutSeconds"]?.GetValue<int>() ?? timeoutSeconds;
        }
        catch (JsonException) { /* use defaults */ }

        if (string.IsNullOrWhiteSpace(text))
            return Results.BadRequest(new { error = "text is required" });

        text = TextSanitizer.StripControlCharacters(text);

        var started = DateTimeOffset.UtcNow;
        state.AppendHistory("user", text);

        // Register reply waiter before enqueuing so we never miss the reply
        var tcs = state.RegisterPendingReply(chatId);

        var update = new PendingUpdate(
            UpdateId: state.NextUpdateId(),
            MessageId: state.NextMessageId(),
            ChatId: chatId,
            UserId: userId,
            Username: username,
            Text: text);

        await state.EnqueueUpdateAsync(update, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var reply = await tcs.Task.WaitAsync(timeoutCts.Token);
            var elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;

            return Results.Ok(new
            {
                reply,
                elapsed_ms = elapsedMs
            });
        }
        catch (OperationCanceledException)
        {
            return Results.Json(
                new { error = $"Bot did not reply within {timeoutSeconds}s" },
                statusCode: 504);
        }
    }

    private static IResult GetStatus(ChatState state) =>
        Results.Ok(new
        {
            running = true,
            uptime_seconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
            started_at = StartedAt,
            conversation_turns = state.GetHistory().Count / 2
        });

    private static IResult GetHistory(ChatState state) =>
        Results.Ok(state.GetHistory());

    private static IResult Shutdown(IHostApplicationLifetime lifetime)
    {
        // Trigger graceful shutdown in the background so we can still respond
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            lifetime.StopApplication();
        });

        return Results.Ok(new { stopping = true });
    }
}
