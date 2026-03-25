using System.Text.Json;

namespace SandraMaya.Host.Assistant.ToolCalling;

/// <summary>
/// A tool the AI assistant can invoke during conversation.
/// Each implementation wraps a specific application service.
/// </summary>
public interface IToolHandler
{
    string Name { get; }
    string Description { get; }
    BinaryData ParametersSchema { get; }
    Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to every tool execution so tools can access the
/// current user, session, and original inbound message.
/// </summary>
public sealed class ToolExecutionContext
{
    private string _sessionId;

    public ToolExecutionContext(
        Guid userId,
        string sessionId,
        InboundMessage? originalMessage)
    {
        UserId = userId;
        _sessionId = sessionId;
        OriginalMessage = originalMessage;
    }

    public Guid UserId { get; }

    public string SessionId => _sessionId;

    public InboundMessage? OriginalMessage { get; }

    public void SetSessionId(string sessionId)
    {
        _sessionId = sessionId;
    }
}

/// <summary>
/// Result returned by a tool execution.
/// Content is always a string (JSON or plain text) that gets passed
/// back to the model as a ToolChatMessage.
/// </summary>
public sealed record ToolResult(string Content, bool IsError = false)
{
    public static ToolResult Success(string content) => new(content);
    public static ToolResult Error(string message) => new(message, IsError: true);
    public static ToolResult Json<T>(T value) =>
        new(System.Text.Json.JsonSerializer.Serialize(value, ToolJsonOptions.Default));
}

internal static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
