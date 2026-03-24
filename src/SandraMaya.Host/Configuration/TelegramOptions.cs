using System.ComponentModel.DataAnnotations;

namespace SandraMaya.Host.Configuration;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.telegram.org/";

    [Range(1, 50)]
    public int PollingTimeoutSeconds { get; init; } = 30;

    [Range(1, 300)]
    public int ErrorBackoffSeconds { get; init; } = 5;

    [Range(1, 100)]
    public int MaxUpdatesPerRequest { get; init; } = 25;

    public bool IncludeEditedMessages { get; init; } = true;
}
