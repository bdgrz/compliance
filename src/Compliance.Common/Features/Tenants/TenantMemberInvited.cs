using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-member.invited", 1)]
public sealed record TenantMemberInvited(Uuid TenantId, string EmailAddress, string Affiliation,
    bool Administrator, string TokenHash, DateTimeOffset ExpiresAt, Uuid InvitedBy) : DomainEvent;
