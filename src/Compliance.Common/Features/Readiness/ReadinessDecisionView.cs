using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Management's attributable decision on one assessment; never an audit opinion.</summary>
public sealed record ReadinessDecisionView(Uuid DecisionId, Uuid AssessmentId, string Outcome,
    string Rationale, Uuid DeciderMemberId, ActorReference DecidedBy, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId);
