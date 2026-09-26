using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IPersonDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
