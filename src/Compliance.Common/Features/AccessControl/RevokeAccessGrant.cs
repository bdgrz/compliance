using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.access-grant.revoke", 1)]
public sealed record RevokeAccessGrant(Uuid TenantId, Uuid GrantId)
    : IRequest, IRbacManagementRequest, ICallable;
