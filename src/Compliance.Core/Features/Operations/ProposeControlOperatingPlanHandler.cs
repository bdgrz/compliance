using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proposes a plan for the control's exact current approved version after verifying every
///     holder against source records and checking reviewer independence.
/// </summary>
public sealed class ProposeControlOperatingPlanHandler(IAggregateExecutor executor,
    IAggregateReader reader, OperatingAuthority authority, TimeProvider clock)
    : IRequestHandler<ProposeControlOperatingPlan, ControlOperatingPlanView>
{
    public async ValueTask<Result<ControlOperatingPlanView>> HandleAsync(
        IRequestContext<ProposeControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        var checkedPlan = await OperatingPlanChecks.CheckAsync(reader, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.ControlVersionId, request.Owner,
            request.BackupOwner, request.ReviewerMemberId, ct).ConfigureAwait(false);
        if (!checkedPlan.IsSuccess)
            return Result<ControlOperatingPlanView>.Failure(checkedPlan.Error!);
        var (version, reviewerHoldsWork) = checkedPlan.Value;
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new ControlOperationsLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.ProposePlan(request.ControlId, request.ExpectedRevision,
                    context.RequestId, version, request.Owner, request.BackupOwner,
                    request.ReviewerMemberId, reviewerHoldsWork, request.Cadence,
                    request.EffectiveFrom, request.Rationale, actor.MemberId, actor.Display,
                    clock.GetUtcNow(), waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindPlan(request.ControlId, context.RequestId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
