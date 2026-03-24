using Microsoft.Extensions.Options;
using SandraMaya.Host.Configuration;

namespace SandraMaya.Host.Assistant;

public sealed class PlaceholderAssistantOrchestrator : IAssistantOrchestrator
{
    private readonly IAssistantSessionStore _sessionStore;
    private readonly IOptions<AzureOpenAiOptions> _azureOptions;
    private readonly ILogger<PlaceholderAssistantOrchestrator> _logger;

    public PlaceholderAssistantOrchestrator(
        IAssistantSessionStore sessionStore,
        IOptions<AzureOpenAiOptions> azureOptions,
        ILogger<PlaceholderAssistantOrchestrator> logger)
    {
        _sessionStore = sessionStore;
        _azureOptions = azureOptions;
        _logger = logger;
    }

    public async Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        var session = await _sessionStore.GetOrCreateAsync(message.Conversation, cancellationToken);
        var azureOptions = _azureOptions.Value;

        _logger.LogInformation(
            "Processing inbound message {MessageId} for session {SessionId}.",
            message.MessageId,
            session.SessionId);

        var replyLines = new List<string>
        {
            "Sandra Maya orchestrator foundation is online.",
            $"Session: {session.SessionId}",
            azureOptions.IsConfigured
                ? $"Azure OpenAI configuration is loaded for deployment '{azureOptions.DeploymentName}' using provider '{azureOptions.ProviderType}'."
                : "Azure OpenAI settings are not fully configured yet, so the placeholder orchestrator handled this message locally."
        };

        if (!string.IsNullOrWhiteSpace(message.EffectiveText))
        {
            replyLines.Add($"Message: {message.EffectiveText}");
        }

        replyLines.Add(
            message.Attachments.Count > 0
                ? $"Attachments: {string.Join(", ", message.Attachments.Select(DescribeAttachment))}"
                : "Attachments: none");

        replyLines.Add("Next step: swap in a Copilot SDK-backed assistant runtime behind IAssistantOrchestrator.");

        return new AssistantTurnResult(
            session.SessionId,
            new[]
            {
                new AssistantReply(string.Join(Environment.NewLine, replyLines))
            });
    }

    private static string DescribeAttachment(InboundAttachment attachment) =>
        string.IsNullOrWhiteSpace(attachment.FileName)
            ? attachment.Kind.ToString()
            : $"{attachment.Kind} ({attachment.FileName})";
}
