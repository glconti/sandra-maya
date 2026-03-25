using System.Text;
using GitHub.Copilot.SDK;
using SandraMaya.Host.Assistant.ToolCalling;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SandraMaya.Host.Assistant;

public sealed class CopilotSdkAssistantOrchestrator : IAssistantOrchestrator
{
    private readonly CopilotConversationSessionManager _sessionManager;
    private readonly CopilotRuntimeConfiguration _runtimeConfiguration;
    private readonly ToolRegistry _toolRegistry;
    private readonly IUserResolutionService _userResolution;
    private readonly SystemPromptBuilder _systemPromptBuilder;
    private readonly ILogger<CopilotSdkAssistantOrchestrator> _logger;

    public CopilotSdkAssistantOrchestrator(
        CopilotConversationSessionManager sessionManager,
        CopilotRuntimeConfiguration runtimeConfiguration,
        ToolRegistry toolRegistry,
        IUserResolutionService userResolution,
        SystemPromptBuilder systemPromptBuilder,
        ILogger<CopilotSdkAssistantOrchestrator> logger)
    {
        _sessionManager = sessionManager;
        _runtimeConfiguration = runtimeConfiguration;
        _toolRegistry = toolRegistry;
        _userResolution = userResolution;
        _systemPromptBuilder = systemPromptBuilder;
        _logger = logger;
    }

    public async Task<AssistantTurnResult> ProcessAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        if (!_runtimeConfiguration.TryResolve(out var settings, out var errorMessage))
        {
            return SingleReply("no-session", errorMessage);
        }

        var userId = await _userResolution.ResolveUserIdAsync(message.Sender, cancellationToken);
        var systemPrompt = await _systemPromptBuilder.BuildAsync(userId, cancellationToken);

        _logger.LogInformation(
            "Processing inbound message {MessageId} for Copilot-backed conversation {ConversationId} (user {UserId}).",
            message.MessageId,
            message.Conversation.ConversationId,
            userId);

        var userText = ResolveUserText(message);
        var toolContext = new ToolExecutionContext(userId, string.Empty, message);

        var createConfig = BuildCreateConfig(settings, systemPrompt, toolContext);
        var resumeConfig = BuildResumeConfig(settings, systemPrompt, toolContext);

        try
        {
            await using var session = await _sessionManager.OpenSessionAsync(
                message.Conversation,
                createConfig,
                resumeConfig,
                cancellationToken);

            toolContext.SetSessionId(session.AssistantSession.SessionId);

            var response = await session.Session.SendAndWaitAsync(
                new MessageOptions
                {
                    Prompt = userText
                },
                timeout: TimeSpan.FromMinutes(2),
                cancellationToken);

            var replyText = string.IsNullOrWhiteSpace(response?.Data?.Content)
                ? "I completed the requested actions."
                : response.Data.Content;

            return new AssistantTurnResult(
                session.AssistantSession.SessionId,
                [new AssistantReply(replyText)]);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error calling Copilot SDK for conversation {ConversationId}.",
                message.Conversation.ConversationId);

            var fallback = settings.UsesByokProvider
                ? "Sorry, I had trouble reaching the Copilot SDK runtime through the configured BYOK provider. Please try again."
                : "Sorry, I had trouble reaching the Copilot SDK runtime. Please try again.";

            return SingleReply("error-session", fallback);
        }
    }

    private SessionConfig BuildCreateConfig(
        CopilotSessionSettings settings,
        string systemPrompt,
        ToolExecutionContext toolContext)
    {
        return new SessionConfig
        {
            ClientName = settings.ClientName,
            Model = settings.Model,
            Provider = settings.Provider,
            WorkingDirectory = settings.WorkingDirectory,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemPrompt
            },
            AvailableTools = [.. _toolRegistry.GetToolNames()],
            Tools = [.. _toolRegistry.GetToolFunctions(toolContext)],
            InfiniteSessions = new InfiniteSessionConfig
            {
                Enabled = true
            },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };
    }

    private ResumeSessionConfig BuildResumeConfig(
        CopilotSessionSettings settings,
        string systemPrompt,
        ToolExecutionContext toolContext)
    {
        return new ResumeSessionConfig
        {
            ClientName = settings.ClientName,
            Model = settings.Model,
            Provider = settings.Provider,
            WorkingDirectory = settings.WorkingDirectory,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemPrompt
            },
            AvailableTools = [.. _toolRegistry.GetToolNames()],
            Tools = [.. _toolRegistry.GetToolFunctions(toolContext)],
            InfiniteSessions = new InfiniteSessionConfig
            {
                Enabled = true
            },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };
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
        new(sessionId, [new AssistantReply(text)]);
}
