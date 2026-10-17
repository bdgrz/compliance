using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the ExaminationEngagementOpened domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.examination-engagement-opened", 1)]
public sealed record ExaminationEngagementOpened(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
