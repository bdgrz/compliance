using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Packages for the authorized engagement or period.</summary>
public sealed record AuditPackageSummaryView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageId, string Name, string Status, DateTimeOffset UpdatedAt);
