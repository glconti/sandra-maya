using System.Text.Json.Serialization;

namespace SandraMaya.ChatCli.Session;

public sealed class ChatSession
{
    [JsonPropertyName("serverPort")]
    public int ServerPort { get; set; }

    [JsonPropertyName("serverPid")]
    public int ServerPid { get; set; }

    [JsonPropertyName("botPid")]
    public int BotPid { get; set; }

    [JsonPropertyName("chatId")]
    public long ChatId { get; set; } = 999;

    [JsonPropertyName("userId")]
    public long UserId { get; set; } = 1;

    [JsonPropertyName("username")]
    public string Username { get; set; } = "agent";

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("botProjectPath")]
    public string BotProjectPath { get; set; } = string.Empty;
}
