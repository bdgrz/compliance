using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditResponseDraftCreated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.audit-response-draft-created", 1)]
public sealed record AuditResponseDraftCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
