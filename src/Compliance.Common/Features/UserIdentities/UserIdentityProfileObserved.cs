using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Current display text asserted by an authenticated provider; never identity or authority.</summary>
[Discriminator("bdgrz.user-identity.profile-observed", 1)]
public sealed record UserIdentityProfileObserved(Uuid UserId, string? DisplayName,
    DateTimeOffset ObservedAt) : DomainEvent;
