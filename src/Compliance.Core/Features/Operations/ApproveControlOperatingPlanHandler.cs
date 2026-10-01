using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records the acting member's own plan approval; HTTP-only. Approval re-verifies that the
///     plan still targets the control's current approved version and reassigns open work.
/// </summary>
public sealed class ApproveControlOperatingPlanHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ApproveControlOperatingPlan, ControlOperatingPlanView>
{
    public async ValueTask<Result<ControlOperatingPlanView>> HandleAsync(
        IRequestContext<ApproveControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOperatingPlanView>.Failure(ControlOperationsSource.ControlNotFound());
        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new ControlOperationsLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var plan = ledger.FindPlan(request.ControlId, request.PlanVersionId);
                var failure = plan is not null && plan.Status ==
                    ControlOperationsLedger.PendingApproval &&
                    (control.IsRetired || control.ApprovedVersion?.VersionId != plan.ControlVersionId ||
                     control.ApprovedVersion.Status != ControlOperationsLedger.Approved)
                        ? CommandFailure.StateConflict(
                            "The plan no longer targets the control's current approved version.")
                        : ledger.ApprovePlan(request.ControlId, request.ExpectedRevision,
                            request.PlanVersionId, request.Rationale, actor.MemberId,
                            actor.Display, clock.GetUtcNow(), waiver);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    ledger.FindPlan(request.ControlId, request.PlanVersionId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
