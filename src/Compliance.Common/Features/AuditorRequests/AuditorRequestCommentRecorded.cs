using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditorRequestCommentRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-comment-recorded", 1)]
public sealed record AuditorRequestCommentRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
