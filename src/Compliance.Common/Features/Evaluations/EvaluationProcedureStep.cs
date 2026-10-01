namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     A planned procedure step. Assertion is design, implementation, or evidence_sufficiency;
///     method is inquiry, inspection, observation, or reperformance.
/// </summary>
public sealed record EvaluationProcedureStep(string Assertion, string Method,
    IReadOnlyList<EvaluationInspectedItem> InspectedItems, string ExpectedCondition);
