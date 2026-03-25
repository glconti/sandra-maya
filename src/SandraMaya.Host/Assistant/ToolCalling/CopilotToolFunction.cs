using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SandraMaya.Host.Assistant.ToolCalling;

public sealed class CopilotToolFunction : AIFunction
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyAdditionalProperties =
        new Dictionary<string, object?>();

    private readonly IToolHandler _handler;
    private readonly ToolExecutionContext _context;
    private readonly JsonElement _schema;

    public CopilotToolFunction(IToolHandler handler, ToolExecutionContext context)
    {
        _handler = handler;
        _context = context;
        _schema = JsonSerializer.Deserialize<JsonElement>(handler.ParametersSchema);
    }

    public override string Name => _handler.Name;

    public override string Description => _handler.Description;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => EmptyAdditionalProperties;

    public override JsonElement JsonSchema => _schema;

    public override JsonSerializerOptions JsonSerializerOptions => ToolJsonOptions.Default;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var jsonArguments = ConvertArguments(arguments);
        var result = await _handler.ExecuteAsync(jsonArguments, _context, cancellationToken);

        if (result.IsError)
        {
            return $"Error: {result.Content}";
        }

        if (TryDeserializeJson(result.Content, out var payload))
        {
            return payload;
        }

        return result.Content;
    }

    private static JsonElement ConvertArguments(AIFunctionArguments arguments)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            values[argument.Key] = argument.Value;
        }

        return JsonSerializer.SerializeToElement(values, ToolJsonOptions.Default);
    }

    private static bool TryDeserializeJson(string content, out object? payload)
    {
        try
        {
            payload = JsonSerializer.Deserialize<object?>(content, ToolJsonOptions.Default);
            return true;
        }
        catch (JsonException)
        {
            payload = null;
            return false;
        }
    }
}
