using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal approval of an expiring exception; HTTP-only.</summary>
public sealed class ExemptAccessExpectationHandler(IAggregateExecutor executor,
    TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptAccessExpectation, AccessExpectationExceptionView>
{
    public async ValueTask<Result<AccessExpectationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessExpectation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                request.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.RecordException(context.RequestId,
                request.ExpectationId, request.ExpectedLedgerRevision, request.ProviderSubjectId,
                request.ProviderEntitlementId, request.Rationale, request.ExpiresAt,
                actor.Reference, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
