using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the ComplianceIncidentRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.incidents.compliance-incident-recorded", 1)]
public sealed record ComplianceIncidentRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
