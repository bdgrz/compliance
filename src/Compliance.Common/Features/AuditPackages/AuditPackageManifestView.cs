using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Exact included records, versions, evidence references, and integrity metadata.</summary>
public sealed record AuditPackageManifestView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageManifestId, IReadOnlyList<Uuid> IncludedRecordIds, string Integrity, DateTimeOffset CreatedAt);
