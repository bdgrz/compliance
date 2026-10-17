using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Period lifecycle, dates, and governing program.</summary>
public sealed record ObservationPeriodView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId, DateTimeOffset ObservedAt, string Description, string Severity, IReadOnlyList<Uuid> EvidenceArtifactIds);
