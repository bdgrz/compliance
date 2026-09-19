using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-invitation.accepted", 1)]
public sealed record TenantInvitationAccepted(Uuid TenantId, Uuid UserId, string EmailAddress,
    string Affiliation, bool Administrator) : DomainEvent;
