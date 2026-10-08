using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Explicitly identifies authoring or approval of a client's management record.</summary>
public interface IClientManagementMutationRequest : IRequestBase
{
    Uuid TenantId { get; }
}
