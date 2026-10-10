using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

[Discriminator("bdgrz.work.digest_dispatch.attempt_started", 1)]
public sealed record WorkDigestDispatchAttemptStarted(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, int Attempt, DateTimeOffset StartedAt) : DomainEvent;
