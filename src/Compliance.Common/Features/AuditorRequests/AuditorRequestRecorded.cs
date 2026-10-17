using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditorRequestRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-recorded", 1)]
public sealed record AuditorRequestRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
