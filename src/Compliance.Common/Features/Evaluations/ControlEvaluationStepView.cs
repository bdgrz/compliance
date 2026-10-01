using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>A frozen procedure step and its result in the current round, if recorded.</summary>
public sealed record ControlEvaluationStepView(Uuid StepId, int Index, string Assertion,
    string Method, IReadOnlyList<EvaluationInspectedItem> InspectedItems,
    string ExpectedCondition, EvaluationStepResultView? Result);
