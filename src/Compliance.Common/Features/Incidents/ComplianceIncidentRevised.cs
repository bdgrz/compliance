using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Records the ComplianceIncidentRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.incidents.compliance-incident-revised", 1)]
public sealed record ComplianceIncidentRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
