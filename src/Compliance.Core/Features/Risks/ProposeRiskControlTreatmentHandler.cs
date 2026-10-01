using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Asserts that the exact current approved version of a same-program control treats a risk
///     whose chosen treatment is mitigate. The Portia request ID identifies the assertion.
/// </summary>
public sealed class ProposeRiskControlTreatmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, ControlActivationSource controls, TimeProvider clock)
    : IRequestHandler<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>
{
    public async ValueTask<Result<RiskControlTreatmentRegistration>> HandleAsync(
        IRequestContext<ProposeRiskControlTreatment> context, CancellationToken ct)
    {
        var request = context.Request;
        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
            request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
        if (!risk.IsSuccess)
            return Result<RiskControlTreatmentRegistration>.Failure(risk.Error);
        var control = await controls.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!control.IsSuccess)
            return Result<RiskControlTreatmentRegistration>.Failure(control.Error);
        if (control.Value.ApprovedVersion is not { Status: "approved" } current ||
            current.VersionId != request.ControlVersionId)
            return Result<RiskControlTreatmentRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "A control treatment must name the control's current approved version."));
        var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId,
            request.RiskId), ct).ConfigureAwait(false);
        var actor = RiskActor.From(context.Actor, request.TenantId);
        return await executor.ExecuteAsync(new RiskGovernanceLedger(request.TenantId,
                request.ProgramId),
            ledger =>
            {
                var failure = ledger.ProposeControlTreatment(request.RiskId,
                    request.ExpectedRevision, context.RequestId, evaluation.Treatment?.Kind,
                    request.ControlId, request.ControlVersionId, request.Rationale,
                    actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display),
                    clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    new RiskControlTreatmentRegistration(context.RequestId,
                        ledger.RevisionOf(request.RiskId)));
            }, context, ct).ConfigureAwait(false);
    }
}
