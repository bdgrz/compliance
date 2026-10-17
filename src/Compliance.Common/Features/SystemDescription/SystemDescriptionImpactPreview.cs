using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Records and baseline references affected by a proposed successor.</summary>
public sealed record SystemDescriptionImpactPreview(Uuid TenantId, Uuid ProgramId, string Summary, IReadOnlyList<Uuid> AffectedRecordIds, DateTimeOffset CalculatedAt);
