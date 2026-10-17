using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Incidents within authorized program/period scope.</summary>
public sealed record ComplianceIncidentSummaryView(Uuid TenantId, Uuid ProgramId, Uuid ComplianceIncidentId, string Name, string Status, DateTimeOffset UpdatedAt);
