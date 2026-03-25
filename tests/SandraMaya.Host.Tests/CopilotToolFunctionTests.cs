using System.Text.Json;
using Microsoft.Extensions.AI;
using SandraMaya.Host.Assistant;
using SandraMaya.Host.Assistant.ToolCalling;

namespace SandraMaya.Host.Tests;

public sealed class CopilotToolFunctionTests
{
    [Fact]
    public async Task InvokeAsync_ForwardsArgumentsAndContextToExistingToolHandler()
    {
        var handler = new RecordingToolHandler();
        var context = new ToolExecutionContext(Guid.NewGuid(), "session-123", null);
        var function = new CopilotToolFunction(handler, context);
        var arguments = new AIFunctionArguments
        {
            ["name"] = "Sandra",
            ["count"] = 2
        };

        var result = await function.InvokeAsync(arguments);

        Assert.NotNull(handler.LastArguments);
        Assert.Equal("Sandra", handler.LastArguments!.Value.GetProperty("name").GetString());
        Assert.Equal(2, handler.LastArguments.Value.GetProperty("count").GetInt32());
        Assert.Same(context, handler.LastContext);

        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvokeAsync_PreservesErrorSignal_WhenErrorContentIsJson()
    {
        var handler = new JsonErrorToolHandler();
        var function = new CopilotToolFunction(handler, new ToolExecutionContext(Guid.NewGuid(), "session-123", null));

        var result = await function.InvokeAsync(new AIFunctionArguments());

        var error = Assert.IsType<string>(result);
        Assert.StartsWith("Error: ", error, StringComparison.Ordinal);
        Assert.Contains("\"ok\":false", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_UsesUpdatedSessionId_WhenContextIsBoundAfterFunctionCreation()
    {
        var handler = new RecordingToolHandler();
        var context = new ToolExecutionContext(Guid.NewGuid(), string.Empty, null);
        var function = new CopilotToolFunction(handler, context);

        context.SetSessionId("session-456");

        await function.InvokeAsync(new AIFunctionArguments
        {
            ["name"] = "Sandra",
            ["count"] = 1
        });

        Assert.NotNull(handler.LastContext);
        Assert.Equal("session-456", handler.LastContext!.SessionId);
    }

    private sealed class RecordingToolHandler : IToolHandler
    {
        public string Name => "recording_tool";

        public string Description => "Records invocations.";

        public BinaryData ParametersSchema => BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {
                "name": { "type": "string" },
                "count": { "type": "integer" }
              },
              "required": ["name", "count"]
            }
            """);

        public JsonElement? LastArguments { get; private set; }

        public ToolExecutionContext? LastContext { get; private set; }

        public Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            LastArguments = arguments.Clone();
            LastContext = context;

            return Task.FromResult(ToolResult.Json(new
            {
                status = "ok"
            }));
        }
    }

    private sealed class JsonErrorToolHandler : IToolHandler
    {
        public string Name => "json_error_tool";

        public string Description => "Returns a JSON-shaped error.";

        public BinaryData ParametersSchema => BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {}
            }
            """);

        public Task<ToolResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ToolResult.Error("""{"ok":false,"error":"partial failure"}"""));
    }
}
