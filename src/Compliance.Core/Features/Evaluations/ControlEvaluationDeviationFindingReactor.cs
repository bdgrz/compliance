using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     Routes every submitted material evaluation deviation into the shared finding path. The
///     finding ID derives from the deviation, so replay and resubmission never duplicate one.
/// </summary>
public sealed partial class ControlEvaluationDeviationFindingReactor(
    IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("control-evaluations"), WorkloadName),
      IReactorHandler<ControlEvaluationSubmitted>
{
    public const string WorkloadName = "ControlEvaluationDeviationFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlEvaluationSubmitted> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        foreach (var deviation in ev.Submission.Deviations.Where(static deviation =>
                     deviation.Classification == ControlEvaluationLedger.Material))
            await bus.SendReactionAsync(new RaiseEvaluationDeviationFinding(ev.TenantId,
                ev.ProgramId, Uuid.CreateVersion5(deviation.DeviationId, "finding"), ev.ControlId,
                ev.EvaluationId, deviation.DeviationId), context, ct).ConfigureAwait(false);
    }
}
