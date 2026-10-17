using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Population members with classification and provenance.</summary>
public sealed record AuditPopulationRowView(Uuid TenantId, Uuid ProgramId, Uuid AuditPopulationRowId, Uuid AuditPopulationId, string Classification, string SourceReference);
