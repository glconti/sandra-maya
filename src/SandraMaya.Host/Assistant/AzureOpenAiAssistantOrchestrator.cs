using System.ClientModel;
using System.Collections.Concurrent;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SandraMaya.Host.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SandraMaya.Host.Assistant;

public sealed class AzureOpenAiAssistantOrchestrator : IAssistantOrchestrator
{
    private const string SystemPrompt =
        "You are Sandra Maya, a personal AI assistant available through Telegram. " +
        "You are helpful, concise, and friendly. " +
        "You assist your user with job searching in the Zurich area (Switzerland), CV management, " +
        "job application tracking, and any other personal tasks they bring to you. " +
        "You remember the full conversation history within this session. " +
        "When the user sends you a file (like a PDF CV), acknowledge it and let them know what you can do with it. " +
        "Respond in the same language the user writes in.";

    private readonly IAssistantSessionStore _sessionStore;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<AzureOpenAiAssistantOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _histories = new();

    public AzureOpenAiAssistantOrchestrator(
        IAssistantSessionStore sessionStore,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiAssistantOrchestrator> logger)
    {
        _sessionStore = sessionStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return SingleReply(
                "no-session",
                "Azure OpenAI is not configured. Please set AzureOpenAi:BaseUrl, AzureOpenAi:ApiKey, and AzureOpenAi:DeploymentName.");
        }

        var session = await _sessionStore.GetOrCreateAsync(message.Conversation, cancellationToken);

        _logger.LogInformation(
            "Processing inbound message {MessageId} for session {SessionId}.",
            message.MessageId,
            session.SessionId);

        var history = _histories.GetOrAdd(session.SessionId, _ => new List<ChatMessage>());

        if (history.Count == 0)
        {
            history.Add(new SystemChatMessage(SystemPrompt));
        }

        var userText = ResolveUserText(message);
        history.Add(new UserChatMessage(userText));

        try
        {
            var client = new AzureOpenAIClient(
                new Uri(_options.BaseUrl!),
                new AzureKeyCredential(_options.ApiKey!));

            var chatClient = client.GetChatClient(_options.DeploymentName!);
            var response = await chatClient.CompleteChatAsync(history, cancellationToken: cancellationToken);
            var replyText = response.Value.Content[0].Text;

            history.Add(new AssistantChatMessage(replyText));

            return new AssistantTurnResult(session.SessionId, new[] { new AssistantReply(replyText) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI for session {SessionId}.", session.SessionId);
            // Remove the unanswered user message so history stays consistent
            history.RemoveAt(history.Count - 1);
            return SingleReply(session.SessionId, "Sorry, I had trouble reaching the AI service. Please try again.");
        }
    }

    private static string ResolveUserText(InboundMessage message)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(message.EffectiveText))
        {
            parts.Add(message.EffectiveText);
        }

        foreach (var attachment in message.Attachments)
        {
            var extracted = ExtractText(attachment);
            var descriptor = string.IsNullOrWhiteSpace(attachment.FileName)
                ? attachment.Kind.ToString()
                : attachment.FileName;

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                parts.Add($"[Attached file: {descriptor}]\n{extracted}");
            }
            else
            {
                parts.Add($"[Attached file: {descriptor} ({attachment.ContentType ?? "unknown type"})]");
            }
        }

        return parts.Count > 0
            ? string.Join("\n\n", parts)
            : "I received your message but couldn't read the text.";
    }

    private static string? ExtractText(InboundAttachment attachment)
    {
        if (attachment.Content is not { Length: > 0 } bytes)
        {
            return null;
        }

        var mime = attachment.ContentType ?? string.Empty;

        if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            (attachment.FileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return ExtractPdfText(bytes);
        }

        return null;
    }

    private static string? ExtractPdfText(byte[] bytes)
    {
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            var sb = new StringBuilder();

            foreach (Page page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }

    private static AssistantTurnResult SingleReply(string sessionId, string text) =>
        new(sessionId, new[] { new AssistantReply(text) });
}
