using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public interface IProviderProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
