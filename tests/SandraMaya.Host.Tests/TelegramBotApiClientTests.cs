using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;
using SandraMaya.Host.Telegram;

namespace SandraMaya.Host.Tests;

public sealed class TelegramBotApiClientTests
{
    [Fact]
    public async Task SendChatActionAsync_PostsTelegramChatAction()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telegram.org/")
        };

        var subject = new TelegramBotApiClient(
            httpClient,
            Options.Create(new TelegramOptions { BotToken = "token-123" }));

        await subject.SendChatActionAsync(12345, TelegramChatActions.Typing, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.EndsWith("/bottoken-123/sendChatAction", handler.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"action\":\"typing\"", handler.RequestBody, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":true}""")
            };
        }
    }
}
