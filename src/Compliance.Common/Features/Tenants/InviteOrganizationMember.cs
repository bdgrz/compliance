using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.organization-member.invite", 1)]
public sealed record InviteOrganizationMember(Uuid TenantId, string EmailAddress,
    string BuiltInRole) : IRequest, IRbacManagementRequest, ICallable;
