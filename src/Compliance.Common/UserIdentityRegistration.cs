using Cntryl.Portia;

namespace Bdgrz.Compliance;

/// <summary>Registers a development identity from an email address.</summary>
[Discriminator("bdgrz.developer-user-identity.register", 1)]
public sealed record RegisterDeveloperUser(string EmailAddress) : IRequest<RegisteredUserIdentity>, ICallable;

/// <summary>Registers the authenticated OpenID Connect identity.</summary>
[Discriminator("bdgrz.oidc-user-identity.register", 1)]
public sealed record RegisterOidcUser : IRequest<RegisteredUserIdentity>, ICallable;

/// <summary>The stable identity established by registration.</summary>
public sealed record RegisteredUserIdentity(Uuid UserIdentityId, Uuid UserId, string? EmailAddress);

/// <summary>Records the provider-neutral identity linked to a platform user.</summary>
[Discriminator("bdgrz.user-identity.registered", 1)]
public sealed record UserIdentityRegistered(
    Uuid UserId,
    string Provider,
    string Identifier,
    string? EmailAddress) : DomainEvent;
