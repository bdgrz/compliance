using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Frozen population metadata, source, and reconciliation state.</summary>
public sealed record AuditPopulationView(Uuid TenantId, Uuid ProgramId, Uuid AuditPopulationId, string Status, long Revision, DateTimeOffset UpdatedAt);
