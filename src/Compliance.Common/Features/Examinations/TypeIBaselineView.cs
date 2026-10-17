using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Frozen baseline manifest, exact source versions, and verification state.</summary>
public sealed record TypeIBaselineView(Uuid TenantId, Uuid ProgramId, Uuid TypeIBaselineId, string Status, long Revision, DateTimeOffset UpdatedAt);
