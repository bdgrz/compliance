using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlDecisionWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, bool IsApproved, bool HasOpenDraft, bool IsRetirement,
    Uuid? PendingTargetId, Uuid? PendingAuthorMemberId, Uuid? AcceptedReviewDecisionId,
    Uuid? AcceptedReviewerMemberId, Uuid? LatestReviewDecisionId,
    DateTimeOffset? PendingDecisionChangedAt, ResponsibilityAssignmentView[] Assignments);
