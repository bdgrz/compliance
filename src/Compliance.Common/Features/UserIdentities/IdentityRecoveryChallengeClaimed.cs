using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Binds an email proof challenge to one old and one replacement identity.</summary>
[Discriminator("bdgrz.identity-recovery.challenge-claimed", 1)]
public sealed record IdentityRecoveryChallengeClaimed(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    Uuid OldIdentityId,
    Uuid ReplacementIdentityId) : DomainEvent;
