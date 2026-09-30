using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     One rule's result for one criterion: rule_met or gap. A met rule states only that the
///     recorded inputs satisfy the product rule; it never states that the criterion is satisfied.
/// </summary>
public sealed record ReadinessFindingView(string CriterionIdentifier, string Category,
    string Summary, string RuleId, string Outcome, string Explanation,
    IReadOnlyList<ReadinessSourceReference> Sources, Uuid? GapId);
