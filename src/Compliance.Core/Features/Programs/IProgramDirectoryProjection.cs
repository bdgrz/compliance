using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProgramDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
