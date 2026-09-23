using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.challenge-delivery-failed", 1)]
public sealed record EmailChallengeDeliveryFailed(Uuid ChallengeId, string FailureCode,
    DateTimeOffset FailedAt) : DomainEvent;
