using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Audits successful authentication through a registered provider identity.</summary>
[Discriminator("bdgrz.user-identity.authenticated", 1)]
public sealed record UserIdentityAuthenticated(
    Uuid UserId,
    string Provider,
    string Identifier) : DomainEvent;
