using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class SetCriteriaTextOverlayHandler(IAggregateExecutor executor,
    ICriteriaCatalog catalog, TimeProvider clock)
    : IRequestHandler<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>
{
    public ValueTask<Result<CriteriaTextOverlayRegistration>> HandleAsync(
        IRequestContext<SetCriteriaTextOverlay> context, CancellationToken ct)
    {
        var request = context.Request;
        if (catalog.GetEntry(request.EditionId, request.Identifier) is null)
            return ValueTask.FromResult(Result<CriteriaTextOverlayRegistration>.Failure(
                new RequestError(RequestErrorKind.NotFound, "The criterion was not found.")));

        var actor = Actor(context);
        return executor.ExecuteAsync(new CriteriaTextOverlayLedger(request.TenantId,
                request.EditionId), ledger => AggregateOutcome.CommitOnSuccess(ledger.Set(
                request.Identifier, context.RequestId, request.ExpectedRevision, request.Content,
                actor, clock.GetUtcNow())), context, ct);
    }

    static ActorReference Actor(IRequestContext<SetCriteriaTextOverlay> context)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "ProgramManagementAuthorizer must reject this actor.");
        return ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}
