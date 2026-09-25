using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public interface ISnapshotDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
