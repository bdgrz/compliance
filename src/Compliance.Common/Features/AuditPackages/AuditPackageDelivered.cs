using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the AuditPackageDelivered domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpackages.audit-package-delivered", 1)]
public sealed record AuditPackageDelivered(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
