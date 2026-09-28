using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Starts recovery using a separately verified email channel.</summary>
[Discriminator("bdgrz.identity-recovery.challenge-issued", 1)]
public sealed record IdentityRecoveryChallengeIssued(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    string TokenKeyId) : DomainEvent;
