using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public interface IReadinessDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
