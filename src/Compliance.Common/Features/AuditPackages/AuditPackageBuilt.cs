using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the AuditPackageBuilt domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-built", 1)]
public sealed record AuditPackageBuilt(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
