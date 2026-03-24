using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SandraMaya.Host.Assistant.ToolCalling;
using SandraMaya.Host.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SandraMaya.Host.Assistant;

public sealed class AzureOpenAiAssistantOrchestrator : IAssistantOrchestrator
{
    private const int MaxToolIterations = 10;

    private readonly IAssistantSessionStore _sessionStore;
    private readonly ToolRegistry _toolRegistry;
    private readonly IUserResolutionService _userResolution;
    private readonly SystemPromptBuilder _systemPromptBuilder;
    private readonly ConversationHistoryStore _historyStore;
    private readonly AzureOpenAiOptions _options;
    private readonly ILogger<AzureOpenAiAssistantOrchestrator> _logger;

    public AzureOpenAiAssistantOrchestrator(
        IAssistantSessionStore sessionStore,
        ToolRegistry toolRegistry,
        IUserResolutionService userResolution,
        SystemPromptBuilder systemPromptBuilder,
        ConversationHistoryStore historyStore,
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiAssistantOrchestrator> logger)
    {
        _sessionStore = sessionStore;
        _toolRegistry = toolRegistry;
        _userResolution = userResolution;
        _systemPromptBuilder = systemPromptBuilder;
        _historyStore = historyStore;
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
        var userId = await _userResolution.ResolveUserIdAsync(message.Sender, cancellationToken);

        _logger.LogInformation(
            "Processing inbound message {MessageId} for session {SessionId} (user {UserId}).",
            message.MessageId,
            session.SessionId,
            userId);

        var history = _historyStore.GetOrCreate(session.SessionId);

        if (history.Count == 0)
        {
            var systemPrompt = await _systemPromptBuilder.BuildAsync(userId, cancellationToken);
            history.Add(new SystemChatMessage(systemPrompt));
        }

        var userText = ResolveUserText(message);
        history.Add(new UserChatMessage(userText));

        try
        {
            var client = new AzureOpenAIClient(
                new Uri(_options.BaseUrl!),
                new AzureKeyCredential(_options.ApiKey!));

            var chatClient = client.GetChatClient(_options.DeploymentName!);

            var options = new ChatCompletionOptions();
            foreach (var tool in _toolRegistry.GetChatTools())
            {
                options.Tools.Add(tool);
            }

            var completion = (await chatClient.CompleteChatAsync(history, options, cancellationToken)).Value;

            var context = new ToolExecutionContext(userId, session.SessionId, message);
            var iterations = 0;

            while (completion.FinishReason == ChatFinishReason.ToolCalls && iterations < MaxToolIterations)
            {
                history.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    _logger.LogInformation(
                        "Executing tool {ToolName} (call {ToolCallId}) in session {SessionId}, iteration {Iteration}.",
                        toolCall.FunctionName, toolCall.Id, session.SessionId, iterations + 1);

                    var handler = _toolRegistry.GetHandler(toolCall.FunctionName);
                    if (handler is null)
                    {
                        _logger.LogWarning("Unknown tool requested: {ToolName}", toolCall.FunctionName);
                        history.Add(new ToolChatMessage(toolCall.Id,
                            $"Error: unknown tool '{toolCall.FunctionName}'."));
                        continue;
                    }

                    try
                    {
                        var args = JsonDocument.Parse(toolCall.FunctionArguments).RootElement;
                        var result = await handler.ExecuteAsync(args, context, cancellationToken);
                        history.Add(new ToolChatMessage(toolCall.Id, result.Content));
                    }
                    catch (Exception toolEx)
                    {
                        _logger.LogError(toolEx,
                            "Tool {ToolName} threw an exception in session {SessionId}.",
                            toolCall.FunctionName, session.SessionId);
                        history.Add(new ToolChatMessage(toolCall.Id,
                            $"Error executing tool: {toolEx.Message}"));
                    }
                }

                completion = (await chatClient.CompleteChatAsync(history, options, cancellationToken)).Value;
                iterations++;
            }

            if (iterations >= MaxToolIterations)
            {
                _logger.LogWarning(
                    "Reached max tool iterations ({Max}) for session {SessionId}.",
                    MaxToolIterations, session.SessionId);
            }

            var replyText = completion.Content.Count > 0 && completion.Content[0].Text is not null
                ? completion.Content[0].Text
                : "I completed the requested actions.";

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
