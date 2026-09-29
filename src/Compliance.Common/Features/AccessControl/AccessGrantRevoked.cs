using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.access-grant.revoked", 1)]
public sealed record AccessGrantRevoked(
    Uuid TenantId,
    Uuid GrantId,
    ActorReference RevokedBy,
    DateTimeOffset RevokedAt) : DomainEvent;
