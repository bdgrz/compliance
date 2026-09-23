using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.challenge-delivery-sent", 1)]
public sealed record EmailChallengeDeliverySent(Uuid ChallengeId, DateTimeOffset SentAt) : DomainEvent;
