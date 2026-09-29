using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.tenant.member.suspend", 1)]
public sealed record SuspendMember(Uuid TenantId, Uuid UserId, string Reason)
    : IRequest, IRbacManagementRequest, ICallable;
