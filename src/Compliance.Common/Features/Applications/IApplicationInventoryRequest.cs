using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}
