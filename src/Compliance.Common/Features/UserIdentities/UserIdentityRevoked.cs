using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records replacement of a provider identity while preserving its platform user.</summary>
[Discriminator("bdgrz.user-identity.revoked", 1)]
public sealed record UserIdentityRevoked(
    Uuid UserIdentityId,
    Uuid UserId,
    Uuid ReplacementIdentityId,
    DateTimeOffset RevokedAt) : DomainEvent;
