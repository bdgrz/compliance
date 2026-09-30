using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The accountable owner, target date, and action planned for a gap.</summary>
public sealed record ReadinessGapPlanView(Uuid GapId, Uuid OwnerMemberId, DateOnly TargetDate,
    string Action, ActorReference PlannedBy, DateTimeOffset PlannedAt);
