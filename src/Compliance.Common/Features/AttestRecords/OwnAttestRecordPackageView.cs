using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Required record package manifest and delivery readiness.</summary>
public sealed record OwnAttestRecordPackageView(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordPackageId, string Status, long Revision, DateTimeOffset UpdatedAt, Uuid? ManifestId);
