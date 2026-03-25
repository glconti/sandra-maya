using System.Collections.Concurrent;
using System.Reflection;
using GitHub.Copilot.SDK;
using SandraMaya.Host.Assistant;

namespace SandraMaya.Host.Tests;

public sealed class ActiveAssistantTurnRegistryTests
{
    [Fact]
    public void TryConsumeStopRequest_ReturnsTrueOnce_WhenStopWasRecorded()
    {
        var subject = new InMemoryActiveAssistantTurnRegistry();
        var conversation = new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1");
        var stopRequests = GetStopRequests(subject);

        stopRequests[conversation.ToKey()] = 0;

        Assert.True(subject.TryConsumeStopRequest(conversation));
        Assert.False(subject.TryConsumeStopRequest(conversation));
    }

    [Fact]
    public void Track_ClearsStaleStopRequest_WhenNewTurnStarts()
    {
        var subject = new InMemoryActiveAssistantTurnRegistry();
        var conversation = new ConversationReference(TransportPlatforms.Telegram, "12345", "user-1");
        var stopRequests = GetStopRequests(subject);

        stopRequests[conversation.ToKey()] = 0;

        using var _ = subject.Track(conversation, null!);

        Assert.False(subject.TryConsumeStopRequest(conversation));
    }

    private static ConcurrentDictionary<string, byte> GetStopRequests(InMemoryActiveAssistantTurnRegistry subject)
    {
        var field = typeof(InMemoryActiveAssistantTurnRegistry)
            .GetField("_stopRequests", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to access stop request state.");

        return (ConcurrentDictionary<string, byte>)field.GetValue(subject)!;
    }
}
