using System.Text;
using SandraMaya.ChatCli.Commands;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

return args[0] switch
{
    "start"   => await StartCommand.RunAsync(args[1..]),
    "serve"   => await ServeCommand.RunAsync(args[1..]),   // internal — spawned by start
    "send"    => await SendCommand.RunAsync(args[1..]),
    "stop"    => await StopCommand.RunAsync(args[1..]),
    "status"  => await StatusCommand.RunAsync(args[1..]),
    "history" => await HistoryCommand.RunAsync(args[1..]),
    "--help" or "-h" or "help" => PrintHelp(),
    var unknown => PrintUnknown(unknown),
};

static int PrintHelp()
{
    Console.WriteLine("""
        sandra-chat — interact with a locally running Sandra Maya bot

        Commands:
          start    [--port N] [--chat-id N] [--user NAME] [--bot-project PATH]
                   Start the mock Telegram server and the bot, write session state.

          send     <message> [--json] [--timeout N]
                   Send one message to the bot and wait for its reply.

          stop     Shut down the bot and mock server.

          status   [--json]  Show whether the session is running.

          history  [--json]  Print the full conversation log.

        The real bot is started with Telegram__ApiBaseUrl pointing at the local
        mock server.  Set AzureOpenAi__* environment variables (or .env) before
        running 'start' to get real AI responses.  Without credentials the bot
        uses its built-in placeholder orchestrator.

        Examples:
          sandra-chat start
          sandra-chat send "What jobs are available in Zurich?"
          sandra-chat history --json
          sandra-chat stop
        """);
    return 0;
}

static int PrintUnknown(string cmd)
{
    Console.Error.WriteLine($"Unknown command '{cmd}'. Run 'sandra-chat --help' for usage.");
    return 1;
}
