using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed partial class SnapshotDirectoryProjector(ISnapshotDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("snapshots"), "SnapshotDirectory"),
      IProjectorHandler<SnapshotFrozen>
{
    public ValueTask HandleAsync(SnapshotFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
