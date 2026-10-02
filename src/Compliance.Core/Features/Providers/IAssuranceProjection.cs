using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public interface IAssuranceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
