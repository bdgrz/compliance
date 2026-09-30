using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface ITechnologyInventoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
