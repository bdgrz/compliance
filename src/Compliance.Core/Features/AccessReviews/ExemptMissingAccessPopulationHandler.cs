using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Records the acting member's personal approval of a missing-population exception; HTTP-only.</summary>
public sealed class ExemptMissingAccessPopulationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptMissingAccessPopulation, AccessPopulationExceptionView>
{
    public async ValueTask<Result<AccessPopulationExceptionView>> HandleAsync(
        IRequestContext<ExemptMissingAccessPopulation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                instance.ApplicationId, instance.Id, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.RecordPopulationException(context.RequestId,
                request.ExpectedLedgerRevision, request.Reason, request.ExpiresAt, actor.Reference,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
