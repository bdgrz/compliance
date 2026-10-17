using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Records the NextObservationPeriodStarted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.next-observation-period-started", 1)]
public sealed record NextObservationPeriodStarted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
