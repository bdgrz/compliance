using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Acceptance is a firm professional operation, separate from client administration.</summary>
public interface IServiceEngagementAcceptanceRequest : IRequestBase
{
    Uuid TenantId { get; }
}
