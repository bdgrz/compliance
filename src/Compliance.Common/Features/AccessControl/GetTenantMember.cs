using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.tenant.member.get", 1)]
public sealed record GetTenantMember(Uuid TenantId, Uuid UserId)
    : IRequest<TenantMembershipView>, IRbacManagementRequest, ICallable;
