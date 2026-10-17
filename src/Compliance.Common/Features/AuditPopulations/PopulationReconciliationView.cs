using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Reconciliation totals, exceptions, and resolution state.</summary>
public sealed record PopulationReconciliationView(Uuid TenantId, Uuid ProgramId, Uuid PopulationReconciliationId, string Status, IReadOnlyList<string> Findings, DateTimeOffset CalculatedAt);
