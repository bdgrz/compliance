using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Package lifecycle, build result, validation, disclosure, and delivery state.</summary>
public sealed record AuditPackageView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageId, string Status, long Revision, DateTimeOffset UpdatedAt, Uuid? ManifestId);
