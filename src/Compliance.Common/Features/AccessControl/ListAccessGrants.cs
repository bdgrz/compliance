using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.access-grant.list", 1)]
public sealed record ListAccessGrants(Uuid TenantId)
    : IRequest<AccessGrantSetView>, IRbacManagementRequest, ICallable;
