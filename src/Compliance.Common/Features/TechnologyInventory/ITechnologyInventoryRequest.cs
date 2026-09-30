using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface ITechnologyInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}
