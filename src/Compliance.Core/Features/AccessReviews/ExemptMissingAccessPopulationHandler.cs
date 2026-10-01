using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal approval of a missing-population exception; HTTP-only.</summary>
public sealed class ExemptMissingAccessPopulationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<ExemptMissingAccessPopulation, AccessPopulationExceptionView>
{
    public async ValueTask<Result<AccessPopulationExceptionView>> HandleAsync(
        IRequestContext<ExemptMissingAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false) is null)
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.RecordPopulationException(context.RequestId,
                request.ExpectedLedgerRevision, request.Reason, request.ExpiresAt, actor.Reference,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
