using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A retained preview, never a production engagement acceptance or personal sign-off.</summary>
public sealed record IndependenceEvaluationView(Uuid EvaluationId, Uuid ClientTenantId,
    long RuleSetVersion, IndependenceRuleVersionView EvaluatedRules, DateOnly ExaminationPeriodStart, string DecisionCode, string? Outcome,
    IReadOnlyList<NonattestServiceView> CompleteServiceHistory,
    IReadOnlyList<Uuid> ConsideredServiceRecordIds, ActorReference Actor, DateTimeOffset RecordedAt,
    bool ProductionAcceptanceBlocked);
