using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditResponseDraftRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.audit-response-draft-revised", 1)]
public sealed record AuditResponseDraftRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
