using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface IPopulationSnapshotDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
