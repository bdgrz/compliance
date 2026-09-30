using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>An unmet readiness rule with a stable identity across reassessments.</summary>
public sealed record ReadinessGapView(Uuid GapId, string Kind, string Subject, string RuleId,
    string Explanation, IReadOnlyList<ReadinessSourceReference> Sources,
    ReadinessGapPlanView? Plan = null);
