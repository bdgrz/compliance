using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

[Discriminator("bdgrz.work.digest_dispatch.scheduled", 1)]
public sealed record WorkDigestDispatchScheduled(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt, Uuid MessageId,
    DateTimeOffset RetryDeadline, DateTimeOffset RecordedAt) : DomainEvent;
