using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.access-grant.issued", 1)]
public sealed record AccessGrantIssued(Uuid TenantId, Uuid GrantId, AccessGrantTerms Terms,
    Uuid? MembershipEpisodeId = null) : DomainEvent;
