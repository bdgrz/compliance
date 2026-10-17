using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the AuditPackageDisclosureApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-disclosure-approved", 1)]
public sealed record AuditPackageDisclosureApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
