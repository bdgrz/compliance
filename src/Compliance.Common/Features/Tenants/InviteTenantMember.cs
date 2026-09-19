using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-member.invite", 1)]
public sealed record InviteTenantMember(Uuid TenantId, string EmailAddress, string Affiliation,
    bool Administrator = false) : IRequest, ICallable, IPlatformOperatorRequest;
