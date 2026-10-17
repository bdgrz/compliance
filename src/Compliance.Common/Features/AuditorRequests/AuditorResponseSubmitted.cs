using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditorResponseSubmitted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.auditor-response-submitted", 1)]
public sealed record AuditorResponseSubmitted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
