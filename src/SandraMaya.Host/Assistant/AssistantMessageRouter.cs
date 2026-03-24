namespace SandraMaya.Host.Assistant;

public sealed class AssistantMessageRouter : IInboundMessageRouter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboundMessageDispatcher _outboundMessageDispatcher;
    private readonly ILogger<AssistantMessageRouter> _logger;

    public AssistantMessageRouter(
        IServiceScopeFactory scopeFactory,
        IOutboundMessageDispatcher outboundMessageDispatcher,
        ILogger<AssistantMessageRouter> logger)
    {
        _scopeFactory = scopeFactory;
        _outboundMessageDispatcher = outboundMessageDispatcher;
        _logger = logger;
    }

    public async Task RouteAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAssistantOrchestrator>();
        var result = await orchestrator.ProcessAsync(message, cancellationToken);

        if (result.Replies.Count == 0)
        {
            _logger.LogDebug("Assistant session {SessionId} produced no replies.", result.SessionId);
            return;
        }

        foreach (var reply in result.Replies)
        {
            await _outboundMessageDispatcher.DispatchAsync(
                new OutboundMessage(message.Conversation, reply.Text),
                cancellationToken);
        }
    }
}
