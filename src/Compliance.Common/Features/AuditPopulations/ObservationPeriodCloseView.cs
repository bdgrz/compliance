using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Frozen close manifest, verification, and amendment history.</summary>
public sealed record ObservationPeriodCloseView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodCloseId, DateTimeOffset ObservedAt, string Description, string Severity, IReadOnlyList<Uuid> EvidenceArtifactIds);
