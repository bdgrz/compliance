using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the DeliveredEngagementHoldReleased domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.attestrecords.delivered-engagement-hold-released", 1)]
public sealed record DeliveredEngagementHoldReleased(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
