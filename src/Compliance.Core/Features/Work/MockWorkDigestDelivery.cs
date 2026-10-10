using System.Collections.Concurrent;

namespace Bdgrz.Compliance.Features.Work;

sealed class MockWorkDigestDelivery : IWorkDigestDelivery
{
    readonly ConcurrentDictionary<Cntryl.Portia.Uuid, WorkDigestDeliveryMessage> _messages = new();

    public ValueTask<WorkDigestTransportOutcome> SendAsync(WorkDigestDeliveryMessage message,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ValueTask.FromResult(new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.Unknown, "transport_ambiguous"));
        _messages.TryAdd(message.MessageId, message);
        return ValueTask.FromResult(new WorkDigestTransportOutcome(
            WorkDigestTransportOutcomeKind.Accepted));
    }

    internal bool TryGet(Cntryl.Portia.Uuid messageId, out WorkDigestDeliveryMessage? message) =>
        _messages.TryGetValue(messageId, out message);
}
