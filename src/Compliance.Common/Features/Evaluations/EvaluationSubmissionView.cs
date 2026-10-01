using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     One submitted round: the separate assertion conclusions, the derived overall result, and
///     the exact step results and deviations it rests on. Submissions are never rewritten.
/// </summary>
public sealed record EvaluationSubmissionView(int Round,
    IReadOnlyList<EvaluationAssertionConclusion> Conclusions, string Overall,
    IReadOnlyList<ControlEvaluationStepView> Steps,
    IReadOnlyList<EvaluationDeviationView> Deviations, ActorReference SubmittedBy,
    DateTimeOffset SubmittedAt);
