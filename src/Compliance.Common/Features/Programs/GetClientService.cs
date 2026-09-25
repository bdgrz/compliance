using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.get", 1)]
public sealed record GetClientService(Uuid TenantId, Uuid ServiceId,
    long? MinimumRevision = null)
    : IRequest<ClientServiceView>, ITenantAccessRequest, ICallable;
