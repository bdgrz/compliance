using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A retained source-change assessment; it can never refresh accepted professional authority.</summary>
public sealed record ServiceIndependenceReevaluationView(Uuid ReevaluationId, Uuid TenantId,
    Uuid CausalRequestId, Uuid ServiceRecordId, long SourceSequence,
    Uuid EngagementId, long AcceptanceRevision, string AcceptanceSha256,
    IndependenceRuleVersionView AcceptedRules, DateOnly ExaminationPeriodStart, DateOnly? ExaminationPeriodEnd,
    DateOnly PolicyReferenceDate, string PeriodStartLookbackDecisionCode, bool RequiresDuringPeriodReview,
    IReadOnlyList<NonattestServiceView> CompleteServiceHistory,
    IReadOnlyList<Uuid> ConsideredServiceRecordIds, string DecisionCode, string State,
    bool ProductionAcceptanceBlocked, ActorReference SourceActor, DateTimeOffset RecordedAt);
