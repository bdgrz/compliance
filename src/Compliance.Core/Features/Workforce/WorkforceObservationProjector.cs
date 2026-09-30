using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class WorkforceObservationProjector(IWorkforceObservationProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("work-relationships"),
            "WorkforceObservationsV1"),
      IProjectorHandler<WorkRelationshipRecorded>, IProjectorHandler<WorkRelationshipRevised>
{
    public ValueTask HandleAsync(WorkRelationshipRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(WorkRelationshipRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
