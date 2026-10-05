using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record BoundaryDecisionWorkState(Uuid TenantId, Uuid ProgramId,
    Uuid BoundaryId, long SourceRevision, Uuid DraftVersionId, long DraftRevision,
    Uuid DraftAuthorMemberId, DateTimeOffset DraftChangedAt, string? LatestReviewOutcome,
    ResponsibilityAssignmentView[] Assignments);
