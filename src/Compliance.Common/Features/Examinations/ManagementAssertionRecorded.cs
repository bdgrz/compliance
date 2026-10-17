using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records the ManagementAssertionRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.examinations.management-assertion-recorded", 1)]
public sealed record ManagementAssertionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
