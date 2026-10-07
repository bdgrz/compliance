namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A Compliance-owned independence policy decision, independent of transport semantics.</summary>
public sealed class IndependenceDecision
{
    internal IndependenceDecision(IndependenceDecisionCode code, long? ruleSetVersion = null,
        bool? partnerEvaluationRecorded = null, DateOnly? examinationPeriodStart = null,
        int? lookBackMonths = null, IndependenceEvaluationOutcome? evaluationOutcome = null,
        IReadOnlyList<IndependenceServiceAssessment>? consideredServices = null,
        string? ruleId = null, EngagementAssignment? conflictingAssignment = null)
    {
        Code = code;
        RuleId = ruleId;
        ConflictingAssignment = conflictingAssignment;
        RuleSetVersion = ruleSetVersion;
        PartnerEvaluationRecorded = partnerEvaluationRecorded;
        ExaminationPeriodStart = examinationPeriodStart;
        LookBackMonths = lookBackMonths;
        EvaluationOutcome = evaluationOutcome;
        ConsideredServices = Array.AsReadOnly(consideredServices?.ToArray() ?? []);
    }

    public IndependenceDecisionCode Code { get; }
    /// <summary>The stable policy rule responsible for an assignment conflict.</summary>
    public string? RuleId { get; }
    /// <summary>The exact historical assignment conflicting with the candidate, scoped to its client.</summary>
    public EngagementAssignment? ConflictingAssignment { get; }
    public bool IsAllowed => Code == IndependenceDecisionCode.Allowed;
    public long? RuleSetVersion { get; }
    public bool? PartnerEvaluationRecorded { get; }
    public DateOnly? ExaminationPeriodStart { get; }
    public int? LookBackMonths { get; }
    public IndependenceEvaluationOutcome? EvaluationOutcome { get; }
    public IReadOnlyList<IndependenceServiceAssessment> ConsideredServices { get; }

    public static IndependenceDecision Allow { get; } = new(IndependenceDecisionCode.Allowed);
}
