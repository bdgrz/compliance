using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Attributed delivery attempt and recipient/provider receipt.</summary>
public sealed record AuditPackageDeliveryView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageDeliveryId, string Status, long Revision, DateTimeOffset UpdatedAt, Uuid? ManifestId);
