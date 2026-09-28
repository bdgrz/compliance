using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Completes replacement and initiates an email notice to the verified address.</summary>
[Discriminator("bdgrz.identity-recovery.challenge-completed", 1)]
public sealed record IdentityRecoveryChallengeCompleted(
    Uuid UserId,
    string EmailAddress,
    Uuid ChallengeId,
    Uuid OldIdentityId,
    Uuid ReplacementIdentityId,
    DateTimeOffset CompletedAt) : DomainEvent;
