using SandraMaya.ChatCli.Server;

namespace SandraMaya.ChatCli.Commands;

/// <summary>
/// Internal command: runs the mock Telegram API server + CLI management API.
/// Spawned as a detached background process by <see cref="StartCommand"/>.
/// Not intended to be called directly by users or AI agents.
/// </summary>
public static class ServeCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        int port = 7777;

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--port" && int.TryParse(args[i + 1], out var p))
                port = p;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning); // keep server output clean
        builder.Services.AddSingleton<ChatState>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        TelegramMockEndpoints.Map(app);
        CliManagementEndpoints.Map(app);

        await app.RunAsync();
        return 0;
    }
}
