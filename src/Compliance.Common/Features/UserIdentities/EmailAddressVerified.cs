using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.verified", 1)]
public sealed record EmailAddressVerified(Uuid UserId, string EmailAddress, Uuid ChallengeId) : DomainEvent;
