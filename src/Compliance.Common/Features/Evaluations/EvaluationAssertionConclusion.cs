namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     The conclusion for one assertion (design, implementation, or evidence_sufficiency):
///     effective, effective_with_exceptions, ineffective, or not_tested. Type II operating
///     effectiveness is never an evaluation assertion.
/// </summary>
public sealed record EvaluationAssertionConclusion(string Assertion, string Conclusion,
    string Rationale);
