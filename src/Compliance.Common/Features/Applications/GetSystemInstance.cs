using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.get", 1)]
public sealed record GetSystemInstance(Uuid TenantId, Uuid ApplicationId, Uuid SystemInstanceId,
    long? MinimumApplicationRevision = null)
    : IRequest<SystemInstanceView>, IApplicationInventoryRequest, ICallable;
