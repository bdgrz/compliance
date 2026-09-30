using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

public sealed record ReadinessAssessmentSummaryView(Uuid AssessmentId, string RuleVersion,
    DateTimeOffset AsOf, int RuleMetCount, int GapCount, ActorReference RunBy,
    DateTimeOffset RunAt, string? DecisionOutcome);
