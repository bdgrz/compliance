using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Marks a request as a read scoped to one tenant, requiring only the tenant-access permission.</summary>
public interface ITenantAccessRequest : IRequestBase
{
    Uuid TenantId { get; }
}
