using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface IInventoryRegisterProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
