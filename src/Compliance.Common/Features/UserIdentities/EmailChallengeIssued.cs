using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.challenge-issued", 1)]
public sealed record EmailChallengeIssued(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    string TokenHash,
    DateTimeOffset ExpiresAt) : DomainEvent;
