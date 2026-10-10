using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

[Discriminator("bdgrz.work.digest_dispatch.unknown_retry_authorized", 1)]
public sealed record WorkDigestUnknownRetryAuthorized(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, Uuid MessageId, int PriorAttempt, Uuid AuthorizedBy,
    string EvidenceReference, string Rationale, DateTimeOffset AuthorizedAt) : DomainEvent;
