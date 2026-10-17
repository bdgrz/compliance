using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Recorded outcome and its authorized, attributable basis.</summary>
public sealed record AuditOutcomeView(Uuid TenantId, Uuid ProgramId, Uuid AuditOutcomeId, string Status, long Revision, DateTimeOffset UpdatedAt);
