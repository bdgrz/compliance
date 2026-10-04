namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A Compliance-owned independence policy decision, independent of transport semantics.</summary>
public sealed class IndependenceDecision
{
    internal IndependenceDecision(IndependenceDecisionCode code, long? ruleSetVersion = null,
        bool? partnerEvaluationRecorded = null, DateOnly? examinationPeriodStart = null,
        int? lookBackMonths = null, IndependenceEvaluationOutcome? evaluationOutcome = null,
        IReadOnlyList<IndependenceServiceAssessment>? consideredServices = null)
    {
        Code = code;
        RuleSetVersion = ruleSetVersion;
        PartnerEvaluationRecorded = partnerEvaluationRecorded;
        ExaminationPeriodStart = examinationPeriodStart;
        LookBackMonths = lookBackMonths;
        EvaluationOutcome = evaluationOutcome;
        ConsideredServices = Array.AsReadOnly(consideredServices?.ToArray() ?? []);
    }

    public IndependenceDecisionCode Code { get; }
    public bool IsAllowed => Code == IndependenceDecisionCode.Allowed;
    public long? RuleSetVersion { get; }
    public bool? PartnerEvaluationRecorded { get; }
    public DateOnly? ExaminationPeriodStart { get; }
    public int? LookBackMonths { get; }
    public IndependenceEvaluationOutcome? EvaluationOutcome { get; }
    public IReadOnlyList<IndependenceServiceAssessment> ConsideredServices { get; }

    public static IndependenceDecision Allow { get; } = new(IndependenceDecisionCode.Allowed);
}
