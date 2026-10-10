using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementAcceptanceView(Uuid TenantId, Uuid EngagementId,
    long ReviewedDraftRevision, long Revision, Uuid ReviewTaskId, Uuid PartnerStaffMemberId,
    Uuid PartnerUserId, long PartnerDirectoryStaffRevision, long PartnerDutyRevision, string AuthorityReference, string? PartnerEvaluationReference,
    Uuid ManagementAcknowledgementId, Uuid? BoundaryId, Uuid? BoundaryVersionId, long? BoundaryRevision,
    Uuid? BoundaryApprovalDecisionId, EngagementAcceptanceSourceTimes SourceTimes, IndependenceRuleVersionView Rules,
    IReadOnlyList<NonattestServiceView> CompleteServiceHistory, string DecisionCode, string EvaluationOutcome,
    IReadOnlyList<Uuid> ConsideredServiceRecordIds,
    IReadOnlyList<EngagementActualAssignmentView> Assignments, string Status,
    ActorReference Actor, DateTimeOffset RecordedAt,
    ActorReference? ChangedBy = null, DateTimeOffset? ChangedAt = null, string? ChangeReason = null)
{
    public PartnerIndependenceEvaluationView? PartnerEvaluation { get; init; }
}
