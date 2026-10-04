using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.tenant.member.deprovision", 1)]
public sealed record DeprovisionMember(Uuid TenantId, Uuid UserId, string Reason)
    : IRequest, IRbacManagementRequest, ICallable;
