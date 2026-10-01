using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed partial class PopulationSnapshotDirectoryProjector(
    IPopulationSnapshotDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("population-snapshots"),
            "PopulationSnapshotDirectoryV2"),
      IProjectorHandler<PopulationSnapshotFrozen>
{
    public ValueTask HandleAsync(PopulationSnapshotFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
