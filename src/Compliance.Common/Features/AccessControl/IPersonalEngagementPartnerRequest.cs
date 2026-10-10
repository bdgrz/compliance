using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Marks personal HTTP operations limited to a currently designated partner for one client.</summary>
public interface IPersonalEngagementPartnerRequest : IRequestBase
{
    Uuid TenantId { get; }
}
