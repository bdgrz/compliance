using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.revision.get", 1)]
public sealed record GetClientServiceRevision(Uuid TenantId, Uuid ServiceId, long Revision)
    : IRequest<ClientServiceRevisionView>, ITenantAccessRequest, ICallable;
