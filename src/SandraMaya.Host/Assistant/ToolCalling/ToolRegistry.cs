using OpenAI.Chat;

namespace SandraMaya.Host.Assistant.ToolCalling;

/// <summary>
/// Collects all registered <see cref="IToolHandler"/> implementations
/// and converts them to Azure OpenAI ChatTool definitions.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers;
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(IEnumerable<IToolHandler> handlers, ILogger<ToolRegistry> logger)
    {
        _handlers = new Dictionary<string, IToolHandler>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;

        foreach (var handler in handlers)
        {
            if (_handlers.TryAdd(handler.Name, handler))
            {
                _logger.LogDebug("Registered tool: {ToolName}", handler.Name);
            }
            else
            {
                _logger.LogWarning("Duplicate tool name ignored: {ToolName}", handler.Name);
            }
        }

        _logger.LogInformation("ToolRegistry initialized with {Count} tools.", _handlers.Count);
    }

    /// <summary>
    /// Returns all tools as Azure OpenAI ChatTool definitions
    /// suitable for <see cref="ChatCompletionOptions.Tools"/>.
    /// </summary>
    public IReadOnlyList<ChatTool> GetChatTools()
    {
        return _handlers.Values
            .Select(h => ChatTool.CreateFunctionTool(
                h.Name,
                h.Description,
                h.ParametersSchema))
            .ToList();
    }

    public IToolHandler? GetHandler(string toolName)
    {
        _handlers.TryGetValue(toolName, out var handler);
        return handler;
    }

    public IReadOnlyList<IToolHandler> GetAllHandlers() =>
        _handlers.Values.ToList();

    public int Count => _handlers.Count;

    /// <summary>
    /// Dynamically registers a tool handler at runtime (used by MCP and capability system).
    /// </summary>
    public void Register(IToolHandler handler)
    {
        if (_handlers.TryAdd(handler.Name, handler))
        {
            _logger.LogInformation("Dynamically registered tool: {ToolName}", handler.Name);
        }
        else
        {
            _logger.LogWarning("Tool already registered, replacing: {ToolName}", handler.Name);
            _handlers[handler.Name] = handler;
        }
    }

    /// <summary>
    /// Removes a dynamically registered tool.
    /// </summary>
    public bool Unregister(string toolName)
    {
        var removed = _handlers.Remove(toolName);
        if (removed)
        {
            _logger.LogInformation("Unregistered tool: {ToolName}", toolName);
        }
        return removed;
    }
}
