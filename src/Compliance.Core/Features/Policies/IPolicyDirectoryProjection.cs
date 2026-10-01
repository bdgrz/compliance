using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public interface IPolicyDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
