using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed class WithdrawCriterionNotApplicableHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<WithdrawCriterionNotApplicable>
{
    public ValueTask<Result> HandleAsync(IRequestContext<WithdrawCriterionNotApplicable> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return executor.ExecuteAsync(new CriterionApplicabilityLedger(request.TenantId,
                request.ProgramId),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Withdraw(request.DecisionId,
                request.ExpectedRevision, request.Rationale, actor, clock.GetUtcNow())),
            context, ct);
    }
}
