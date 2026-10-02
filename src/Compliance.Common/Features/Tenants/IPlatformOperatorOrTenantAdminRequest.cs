using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Marks tenant metadata operations available to platform operators or that tenant's Org Admins.</summary>
public interface IPlatformOperatorOrTenantAdminRequest : IRequestBase
{
    Uuid TenantId { get; }
}
