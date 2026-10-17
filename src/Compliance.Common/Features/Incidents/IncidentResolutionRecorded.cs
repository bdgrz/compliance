using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the IncidentResolutionRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.incidents.incident-resolution-recorded", 1)]
public sealed record IncidentResolutionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
