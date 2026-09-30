using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed class PlanReadinessGapHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<PlanReadinessGap, ReadinessGapPlanView>
{
    public async ValueTask<Result<ReadinessGapPlanView>> HandleAsync(
        IRequestContext<PlanReadinessGap> context, CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return await executor.ExecuteAsync(new ReadinessLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.Plan(request.GapId, request.ExpectedRevision,
                    request.OwnerMemberId, request.TargetDate, request.Action,
                    RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindPlan(request.GapId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
