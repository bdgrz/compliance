using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the SignificantChangeRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.changes.significant-change-recorded", 1)]
public sealed record SignificantChangeRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
