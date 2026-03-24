using System.Diagnostics;
using SandraMaya.ChatCli.Session;

namespace SandraMaya.ChatCli.Commands;

/// <summary>
/// Starts the mock Telegram server and the Sandra Maya Host bot as background processes,
/// then writes session state so subsequent one-shot commands can reconnect.
/// </summary>
public static class StartCommand
{
    private const string DefaultBotProject = "src/SandraMaya.Host/SandraMaya.Host.csproj";

    public static async Task<int> RunAsync(string[] args)
    {
        // --- Parse arguments ---
        int port = FindFreePort();
        long chatId = 999;
        long userId = 1;
        string username = "agent";
        string botProject = DefaultBotProject;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port" when i + 1 < args.Length:
                    port = int.Parse(args[++i]);
                    break;
                case "--chat-id" when i + 1 < args.Length:
                    chatId = long.Parse(args[++i]);
                    break;
                case "--user" when i + 1 < args.Length:
                    username = args[++i];
                    break;
                case "--bot-project" when i + 1 < args.Length:
                    botProject = args[++i];
                    break;
            }
        }

        // --- Check for existing session ---
        var existing = SessionStore.TryLoad();
        if (existing is not null)
        {
            Console.Error.WriteLine(
                $"A session is already running on port {existing.ServerPort} " +
                $"(server PID {existing.ServerPid}). " +
                "Run 'sandra-chat stop' first, or delete ~/.sandra-maya-chat/session.json.");
            return 1;
        }

        // Resolve bot project path relative to cwd
        var resolvedBotProject = Path.IsPathRooted(botProject)
            ? botProject
            : Path.GetFullPath(botProject);

        // --- Start mock server ---
        var cliExe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine CLI exe path.");
        var serverProcess = StartDetachedProcess(
            cliExe,
            $"serve --port {port}",
            workingDir: Directory.GetCurrentDirectory());

        Console.WriteLine($"Starting mock Telegram server on port {port} (PID {serverProcess.Id})...");

        // Wait for server to be ready
        if (!await WaitForHealthAsync(port, timeout: TimeSpan.FromSeconds(15)))
        {
            Console.Error.WriteLine("Mock server did not start within 15 seconds.");
            serverProcess.Kill(entireProcessTree: true);
            return 1;
        }

        Console.WriteLine("Mock server ready.");

        // --- Start Sandra Maya Host bot ---
        var botEnv = new Dictionary<string, string>
        {
            ["Telegram__BotToken"] = "test:sandra-maya-cli",
            ["Telegram__ApiBaseUrl"] = $"http://localhost:{port}/",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",  // bind to random loopback port so no conflict
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development"
        };

        var botProcess = StartBotProcess(resolvedBotProject, botEnv);
        Console.WriteLine($"Starting Sandra Maya bot (PID {botProcess.Id})...");

        // Give the bot a moment to start connecting
        await Task.Delay(2000);

        if (botProcess.HasExited)
        {
            Console.Error.WriteLine($"Bot process exited early with code {botProcess.ExitCode}.");
            serverProcess.Kill(entireProcessTree: true);
            return 1;
        }

        // --- Save session ---
        var session = new ChatSession
        {
            ServerPort = port,
            ServerPid = serverProcess.Id,
            BotPid = botProcess.Id,
            ChatId = chatId,
            UserId = userId,
            Username = username,
            StartedAt = DateTimeOffset.UtcNow,
            BotProjectPath = resolvedBotProject
        };

        SessionStore.Save(session);

        Console.WriteLine($"""
            Session started.
              Mock server : http://localhost:{port}/
              Bot PID     : {botProcess.Id}
              Chat ID     : {chatId}
              Username    : {username}

            Send a message:
              sandra-chat send "Hello, what can you do?"
            """);

        return 0;
    }

    private static Process StartDetachedProcess(string exe, string arguments, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        return Process.Start(psi)
               ?? throw new InvalidOperationException($"Failed to start process: {exe} {arguments}");
    }

    private static Process StartBotProcess(string botProjectPath, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{botProjectPath}\" --no-launch-profile",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        foreach (var (key, value) in env)
            psi.EnvironmentVariables[key] = value;

        return Process.Start(psi)
               ?? throw new InvalidOperationException("Failed to start Sandra Maya bot.");
    }

    private static async Task<bool> WaitForHealthAsync(int port, TimeSpan timeout)
    {
        var url = $"http://localhost:{port}/health";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch { /* not ready yet */ }

            await Task.Delay(300);
        }

        return false;
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
