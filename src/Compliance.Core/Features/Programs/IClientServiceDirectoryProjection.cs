using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IClientServiceDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
