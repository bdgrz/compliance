using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Client-owned service and evaluation records requiring explicit client administration grants.</summary>
public interface IIndependenceAdministrationRequest : IRequestBase
{
    Uuid TenantId { get; }
}
