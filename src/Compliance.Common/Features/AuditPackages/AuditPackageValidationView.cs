using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Validation findings and any separately approved override.</summary>
public sealed record AuditPackageValidationView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageValidationId, string Status, long Revision, DateTimeOffset UpdatedAt, Uuid? ManifestId);
