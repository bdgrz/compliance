using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the AuditPackageValidated domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-validated", 1)]
public sealed record AuditPackageValidated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
