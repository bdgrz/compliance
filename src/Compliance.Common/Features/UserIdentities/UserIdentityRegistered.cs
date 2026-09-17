using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the provider-neutral identity linked to a platform user.</summary>
[Discriminator("bdgrz.user-identity.registered", 1)]
public sealed record UserIdentityRegistered(
    Uuid UserId,
    string Provider,
    string Identifier,
    string? EmailAddress) : DomainEvent;
