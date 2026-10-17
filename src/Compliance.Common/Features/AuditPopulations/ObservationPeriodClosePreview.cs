using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Completeness and unresolved-item report before freezing close.</summary>
public sealed record ObservationPeriodClosePreview(Uuid TenantId, Uuid ProgramId, string Summary, IReadOnlyList<Uuid> AffectedRecordIds, DateTimeOffset CalculatedAt);
