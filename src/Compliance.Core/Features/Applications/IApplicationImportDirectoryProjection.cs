using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationImportDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default);
}
