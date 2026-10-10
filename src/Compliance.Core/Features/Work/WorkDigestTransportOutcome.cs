namespace Bdgrz.Compliance.Features.Work;

enum WorkDigestTransportOutcomeKind
{
    Accepted,
    DefiniteTransientRejection,
    DefinitePermanentRejection,
    Unknown,
}

sealed record WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind Kind,
    string? FailureCode = null);

sealed record WorkDigestDeliveryMessage(Cntryl.Portia.Uuid MessageId,
    string Recipient, string Subject, string TextBody);

interface IWorkDigestDelivery
{
    ValueTask<WorkDigestTransportOutcome> SendAsync(WorkDigestDeliveryMessage message,
        CancellationToken ct);
}
