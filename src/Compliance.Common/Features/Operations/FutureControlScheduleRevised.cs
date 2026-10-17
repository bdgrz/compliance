using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the FutureControlScheduleRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.future-control-schedule-revised", 1)]
public sealed record FutureControlScheduleRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
