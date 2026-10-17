using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Assessed control, risk, and evidence impacts with disposition.</summary>
public sealed record ComplianceImpactAssessmentView(Uuid TenantId, Uuid ProgramId, Uuid ComplianceImpactAssessmentId, string Status, IReadOnlyList<string> Findings, DateTimeOffset CalculatedAt);
