using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the AuditPackageValidationOverrideApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-validation-override-approved", 1)]
public sealed record AuditPackageValidationOverrideApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
