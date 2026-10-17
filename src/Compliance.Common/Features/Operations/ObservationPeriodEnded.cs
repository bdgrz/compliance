using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the ObservationPeriodEnded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.observation-period-ended", 1)]
public sealed record ObservationPeriodEnded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
