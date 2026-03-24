using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SandraMaya.ChatCli.Session;
using SandraMaya.ChatCli.Text;

namespace SandraMaya.ChatCli.Commands;

/// <summary>
/// Sends one message to the running bot and waits for its reply.
/// Prints the reply text (or a JSON envelope with --json) and exits.
/// </summary>
public static class SendCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        string? message = null;
        bool json = false;
        int timeout = 30;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--timeout" when i + 1 < args.Length:
                    timeout = int.Parse(args[++i]);
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                        message = args[i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.Error.WriteLine("Usage: sandra-chat send \"your message\" [--json] [--timeout N]");
            return 1;
        }

        var session = SessionStore.TryLoad();
        if (session is null)
        {
            Console.Error.WriteLine("No active session found. Run 'sandra-chat start' first.");
            return 1;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{session.ServerPort}/"),
            Timeout = TimeSpan.FromSeconds(timeout + 10)
        };

        var payload = new
        {
            text = message,
            chatId = session.ChatId,
            userId = session.UserId,
            username = session.Username,
            timeoutSeconds = timeout
        };

        HttpResponseMessage response;

        try
        {
            response = await client.PostAsJsonAsync("/cli/send", payload);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Could not reach the mock server at port {session.ServerPort}: {ex.Message}");
            Console.Error.WriteLine("Is the session running? Try 'sandra-chat status'.");
            return 1;
        }

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (json)
            {
                Console.WriteLine(body);
            }
            else
            {
                var doc = JsonNode.Parse(body);
                Console.Error.WriteLine(doc?["error"]?.GetValue<string>() ?? "Error from server.");
            }

            return response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ? 2 : 1;
        }

        var result = JsonNode.Parse(body);
        var reply = TextSanitizer.StripControlCharacters(
            result?["reply"]?.GetValue<string>() ?? string.Empty);
        var elapsedMs = result?["elapsed_ms"]?.GetValue<long>() ?? 0;

        if (json)
        {
            var output = new
            {
                reply,
                elapsed_ms = elapsedMs
            };
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        }
        else
        {
            Console.WriteLine(reply);
        }

        return 0;
    }
}
