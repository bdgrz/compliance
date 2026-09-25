using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
