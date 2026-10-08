using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}

/// <summary>
///     A read request whose handler applies restricted application visibility to every returned
///     resource. Scoped restricted-read grants may authorize these requests.
/// </summary>
public interface IRestrictedApplicationResourceReadRequest : IApplicationInventoryRequest
{
}

public interface IApplicationInventoryWriteRequest : IApplicationInventoryRequest, IClientManagementMutationRequest
{
}
