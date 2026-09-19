using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.rbac.member.register", 1)]
public sealed record RegisterMember(Uuid TenantId, Uuid UserId, string Affiliation = "client_personnel")
    : IRequest, IRbacManagementRequest;
