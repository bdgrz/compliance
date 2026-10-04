using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed class ProposeAccessExpectationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ProposeAccessExpectation, AccessExpectationView>
{
    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ProposeAccessExpectation> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                instance.ApplicationId, instance.Id, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        return await executor.ExecuteAsync(new AccessReviewSystemLedger(request.TenantId,
                request.SystemInstanceId),
            ledger => AccessReviewOutcome.From(ledger.Propose(request.ExpectedLedgerRevision,
                context.RequestId, request.RuleKind, request.Parameters, request.Rationale,
                request.EffectiveFrom, request.EffectiveUntil, request.SupersedesExpectationId,
                actor.MemberId, actor.Reference, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
