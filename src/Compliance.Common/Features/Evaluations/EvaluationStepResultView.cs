using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>A step's recorded result: met, not_met, or not_tested, with what was actually inspected.</summary>
public sealed record EvaluationStepResultView(int Round, string Result, string Rationale,
    IReadOnlyList<EvaluationInspectedItem> InspectedItems, ActorReference RecordedBy,
    DateTimeOffset RecordedAt);
