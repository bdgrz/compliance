namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The deterministic output of <see cref="ReadinessRules" /> for one set of inputs.</summary>
public sealed record ReadinessEvaluation(IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
    string InputFingerprint);
