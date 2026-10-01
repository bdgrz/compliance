using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal approval of an expiring exception; HTTP-only.</summary>
public sealed class ExemptAccessExpectationHandler(IAggregateExecutor executor,
    TimeProvider clock)
    : IRequestHandler<ExemptAccessExpectation, AccessExpectationExceptionView>
{
    public async ValueTask<Result<AccessExpectationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessExpectation> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.RecordException(context.RequestId,
                request.ExpectationId, request.ExpectedLedgerRevision, request.ProviderSubjectId,
                request.ProviderEntitlementId, request.Rationale, request.ExpiresAt,
                actor.Reference, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
