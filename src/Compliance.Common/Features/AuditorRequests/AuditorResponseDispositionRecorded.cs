using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditorResponseDispositionRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-disposition-recorded", 1)]
public sealed record AuditorResponseDispositionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
