using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementPartnerReviewContextView(Uuid TenantId, long ClientSequence,
    ServiceEngagementView TargetEngagement, IReadOnlyList<ServiceEngagementView> ClientEngagements,
    IReadOnlyList<ServiceEngagementAcceptanceView> ClientAcceptanceHistory,
    IReadOnlyList<HistoricalEngagementAssignmentView> ClientActualAssignmentHistory,
    IReadOnlyList<NonattestServiceView> CompleteNonattestServices,
    IReadOnlyList<EngagementManagementAcknowledgementView> ClientManagementAcknowledgements,
    IReadOnlyList<EngagementManagementAcknowledgementView> TargetManagementAcknowledgements,
    IndependenceRuleVersionView? ActiveRatifiedRules,
    IReadOnlyList<PartnerIndependenceEvaluationView> ClientPartnerEvaluations);
