using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class ControlOccurrenceWorkItemProjector(
    IControlOccurrenceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-operations"),
        FitzControlOccurrenceWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlOperatingPlanProposed>,
      IProjectorHandler<ControlOperatingPlanApproved>,
      IProjectorHandler<ControlOccurrenceOpened>,
      IProjectorHandler<ControlOccurrenceAttested>,
      IProjectorHandler<ControlOccurrenceReviewed>
{
    public ValueTask HandleAsync(ControlOperatingPlanProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOperatingPlanApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOccurrenceOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOccurrenceAttested ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOccurrenceReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
