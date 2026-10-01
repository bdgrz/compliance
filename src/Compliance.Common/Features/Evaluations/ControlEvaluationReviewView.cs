using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>An independent review decision on one submitted round: accepted, rejected, or changes_requested.</summary>
public sealed record ControlEvaluationReviewView(Uuid DecisionId, int Round, string Decision,
    string Rationale, Uuid ReviewerMemberId, ActorReference ReviewedBy, DateTimeOffset ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId = null);
