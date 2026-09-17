using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Marks a request as an RBAC-management operation scoped to one tenant.</summary>
public interface IRbacManagementRequest : IRequestBase
{
    Uuid TenantId { get; }
}
