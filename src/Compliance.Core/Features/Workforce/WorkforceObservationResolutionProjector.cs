using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class WorkforceObservationResolutionProjector(
    IWorkforceObservationResolutionProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("workforce-observation-resolutions"),
            "WorkforceObservationResolutionsV1"),
      IProjectorHandler<WorkforceObservationResolved>
{
    public ValueTask HandleAsync(WorkforceObservationResolved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
