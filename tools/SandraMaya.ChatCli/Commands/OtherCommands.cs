using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using SandraMaya.ChatCli.Session;
using SandraMaya.ChatCli.Text;

namespace SandraMaya.ChatCli.Commands;

public static class StopCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var session = SessionStore.TryLoad();
        if (session is null)
        {
            Console.Error.WriteLine("No active session found.");
            return 1;
        }

        // Ask the server to stop gracefully first
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await client.PostAsync($"http://localhost:{session.ServerPort}/cli/shutdown",
                content: null);
        }
        catch { /* best-effort */ }

        // Kill processes by PID
        KillProcessIfRunning(session.ServerPid, "mock server");
        KillProcessIfRunning(session.BotPid, "bot");

        SessionStore.Delete();
        Console.WriteLine("Session stopped.");
        return 0;
    }

    private static void KillProcessIfRunning(int pid, string label)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            Console.WriteLine($"Stopped {label} (PID {pid}).");
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"{label} process (PID {pid}) was not running.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not stop {label} (PID {pid}): {ex.Message}");
        }
    }
}

public static class StatusCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        bool json = args.Contains("--json");

        var session = SessionStore.TryLoad();
        if (session is null)
        {
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { running = false }, JsonOptions));
            else
                Console.WriteLine("No active session.");
            return 0;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var serverStatus = await client.GetFromJsonAsync<JsonElement>(
                $"http://localhost:{session.ServerPort}/cli/status");

            if (json)
            {
                var output = new
                {
                    running = true,
                    serverPort = session.ServerPort,
                    serverPid = session.ServerPid,
                    botPid = session.BotPid,
                    chatId = session.ChatId,
                    username = session.Username,
                    startedAt = session.StartedAt,
                    server = serverStatus
                };
                Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
            }
            else
            {
                Console.WriteLine($"""
                    Session running
                      Server     : http://localhost:{session.ServerPort}/ (PID {session.ServerPid})
                      Bot PID    : {session.BotPid}
                      Chat ID    : {session.ChatId}
                      Username   : {session.Username}
                      Started    : {session.StartedAt:u}
                    """);
            }

            return 0;
        }
        catch (HttpRequestException)
        {
            Console.Error.WriteLine(
                $"Session file exists (port {session.ServerPort}) but server is not responding. " +
                "The process may have crashed. Run 'sandra-chat stop' to clean up.");
            return 1;
        }
    }
}

public static class HistoryCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        bool json = args.Contains("--json");

        var session = SessionStore.TryLoad();
        if (session is null)
        {
            Console.Error.WriteLine("No active session found.");
            return 1;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var historyJson = await client.GetStringAsync(
                $"http://localhost:{session.ServerPort}/cli/history");

            if (json)
            {
                Console.WriteLine(TextSanitizer.StripControlCharacters(historyJson));
                return 0;
            }

            var entries = JsonSerializer.Deserialize<JsonElement[]>(historyJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (entries is null || entries.Length == 0)
            {
                Console.WriteLine("(No conversation history yet)");
                return 0;
            }

            foreach (var entry in entries)
            {
                var role = entry.GetProperty("role").GetString() ?? "?";
                var text = TextSanitizer.StripControlCharacters(
                    entry.GetProperty("text").GetString() ?? "");
                var at = entry.TryGetProperty("at", out var atProp) ? atProp.GetString() : null;

                var prefix = role == "user" ? "YOU" : "BOT";
                Console.WriteLine($"[{at ?? ""}] {prefix}: {text}");
                Console.WriteLine();
            }

            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Could not reach server: {ex.Message}");
            return 1;
        }
    }
}
