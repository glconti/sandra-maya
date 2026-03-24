namespace SandraMaya.Host.Assistant;

public sealed class AssistantMessageRouter : IInboundMessageRouter
{
    private readonly IAssistantOrchestrator _assistantOrchestrator;
    private readonly IOutboundMessageDispatcher _outboundMessageDispatcher;
    private readonly ILogger<AssistantMessageRouter> _logger;

    public AssistantMessageRouter(
        IAssistantOrchestrator assistantOrchestrator,
        IOutboundMessageDispatcher outboundMessageDispatcher,
        ILogger<AssistantMessageRouter> logger)
    {
        _assistantOrchestrator = assistantOrchestrator;
        _outboundMessageDispatcher = outboundMessageDispatcher;
        _logger = logger;
    }

    public async Task RouteAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        var result = await _assistantOrchestrator.ProcessAsync(message, cancellationToken);

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
