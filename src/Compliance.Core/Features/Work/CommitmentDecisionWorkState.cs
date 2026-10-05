using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record CommitmentDecisionWorkState(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    string Identifier, long SourceRevision, long? LatestEffectiveRevision,
    Uuid? AcceptedReviewDecisionId, Uuid? AcceptedReviewerMemberId,
    Uuid[] PendingAuthorMemberIds, DateTimeOffset DraftChangedAt,
    ResponsibilityAssignmentView[] Assignments);
