using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class ControlOperatingPlanWorkItemProjector(
    IControlOperatingPlanWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-operations"),
        FitzControlOperatingPlanWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlOperatingPlanProposed>,
      IProjectorHandler<ControlOperatingPlanApproved>
{
    public ValueTask HandleAsync(ControlOperatingPlanProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlOperatingPlanApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
