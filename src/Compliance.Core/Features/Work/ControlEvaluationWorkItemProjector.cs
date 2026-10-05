using Bdgrz.Compliance.Features.Evaluations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class ControlEvaluationWorkItemProjector(
    IControlEvaluationWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-evaluations"),
        FitzControlEvaluationWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlEvaluationStarted>,
      IProjectorHandler<ControlEvaluationStepRecorded>,
      IProjectorHandler<ControlEvaluationDeviationDisposed>,
      IProjectorHandler<ControlEvaluationSubmitted>,
      IProjectorHandler<ControlEvaluationReviewed>
{
    public ValueTask HandleAsync(ControlEvaluationStarted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationStepRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationDeviationDisposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationSubmitted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlEvaluationReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
