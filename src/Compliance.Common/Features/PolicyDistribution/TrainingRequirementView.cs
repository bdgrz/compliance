using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     One immutable training requirement version. <c>Cadence</c> is the M0-D12 rule
///     <c>hire_within_30_days_then_annual</c>.
/// </summary>
public sealed record TrainingRequirementView(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, string Identifier, long Version, long LatestVersion,
    TrainingRequirementContent Content, string ContentSha256, string Cadence,
    ActorReference ChangedBy, DateTimeOffset ChangedAt);
