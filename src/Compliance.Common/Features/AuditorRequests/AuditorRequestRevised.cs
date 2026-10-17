using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the AuditorRequestRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditorrequests.auditor-request-revised", 1)]
public sealed record AuditorRequestRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
