using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Authorized definitions and version state for the period.</summary>
public sealed record AuditPopulationDefinitionView(Uuid TenantId, Uuid ProgramId, Uuid AuditPopulationDefinitionId, string Status, long Revision, DateTimeOffset UpdatedAt);
