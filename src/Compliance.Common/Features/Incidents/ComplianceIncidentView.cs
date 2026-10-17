using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Incident, assessment, resolution, and evidence references.</summary>
public sealed record ComplianceIncidentView(Uuid TenantId, Uuid ProgramId, Uuid ComplianceIncidentId, string Title, string Status, DateTimeOffset OccurredAt, string? Resolution);
