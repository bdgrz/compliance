using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Engagement/period observations, evidence references, and linked findings.</summary>
public sealed record AuditObservationView(Uuid TenantId, Uuid ProgramId, Uuid AuditObservationId, DateTimeOffset ObservedAt, string Description, string Severity, IReadOnlyList<Uuid> EvidenceArtifactIds);
