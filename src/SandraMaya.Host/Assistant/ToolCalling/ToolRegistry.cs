using Microsoft.Extensions.AI;

namespace SandraMaya.Host.Assistant.ToolCalling;

/// <summary>
/// Collects all registered <see cref="IToolHandler"/> implementations
/// and converts them to Copilot SDK tool definitions.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers;
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(
        IEnumerable<IToolHandler> handlers,
        ILogger<ToolRegistry> logger)
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
    /// Returns all tools as Copilot SDK functions scoped to the current invocation context.
    /// </summary>
    public IReadOnlyList<AIFunction> GetToolFunctions(ToolExecutionContext context)
    {
        return GetAllEffectiveHandlers()
            .Select(handler => (AIFunction)new CopilotToolFunction(handler, context))
            .ToList();
    }

    public IReadOnlyList<string> GetToolNames() =>
        GetAllEffectiveHandlers().Select(handler => handler.Name).ToList();

    public IToolHandler? GetHandler(string toolName)
    {
        return GetAllEffectiveHandlers().FirstOrDefault(handler =>
            string.Equals(handler.Name, toolName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<IToolHandler> GetAllHandlers() =>
        GetAllEffectiveHandlers();

    public int Count => GetAllEffectiveHandlers().Count;

    /// <summary>
    /// Dynamically registers a tool handler at runtime.
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

    private IReadOnlyList<IToolHandler> GetAllEffectiveHandlers()
    {
        return _handlers.Values
            .OrderBy(handler => handler.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
