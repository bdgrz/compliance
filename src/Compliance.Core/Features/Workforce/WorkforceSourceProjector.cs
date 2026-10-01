using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed partial class WorkforceSourceProjector(IWorkforceSourceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("workforce-source-observations"), "WorkforceSourcesV1"),
      IProjectorHandler<WorkforceSourceObserved>, IProjectorHandler<WorkforceSourceReconciled>
{
    public ValueTask HandleAsync(WorkforceSourceObserved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(WorkforceSourceReconciled ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
