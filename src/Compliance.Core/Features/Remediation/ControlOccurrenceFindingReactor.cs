using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Routes control-operation exceptions into the shared finding path: a failed or skipped
///     attestation and each action a reviewer requests become a finding whose identity derives
///     from the exact attestation or decision, so replay never duplicates one.
/// </summary>
public sealed partial class ControlOccurrenceFindingReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("control-operations"), WorkloadName),
      IReactorHandler<ControlOccurrenceAttested>, IReactorHandler<ControlOccurrenceReviewed>
{
    public const string WorkloadName = "ControlOccurrenceFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceAttested> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        var attestation = ev.Attestation;
        if (attestation.Result is not (ControlOperationsLedger.Failed or ControlOperationsLedger.Skipped))
            return;
        await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
            Uuid.CreateVersion5(attestation.AttestationId, "finding"), ev.ControlId,
            ev.OccurrenceId, attestation.AttestationId.ToString(), "control_occurrence",
            $"{attestation.Result}: {attestation.Rationale}"), context, ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        var review = ev.Review;
        if (review.Outcome != ControlOperationsLedger.ActionRequested)
            return;
        for (var index = 0; index < review.RequestedActions.Count; index++)
            await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
                Uuid.CreateVersion5(review.DecisionId, "requested-action-" +
                    index.ToString(CultureInfo.InvariantCulture)), ev.ControlId, ev.OccurrenceId,
                review.DecisionId.ToString(), "occurrence_review", review.RequestedActions[index]),
                context, ct).ConfigureAwait(false);
    }
}
