using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record AccessReviewCampaignWorkCampaignState(Uuid TenantId, Uuid ProgramId,
    Uuid CampaignId, long SourceRevision, DateOnly DueOn, DateTimeOffset LaunchedAt,
    bool IsCompleted, int ItemCount);

public sealed record AccessReviewCampaignWorkItemState(Uuid TenantId, Uuid ProgramId,
    Uuid CampaignId, Uuid ItemId, long SourceRevision, DateOnly DueOn,
    DateTimeOffset LaunchedAt, bool IsCompleted, Uuid SystemInstanceId, Uuid? SubjectMemberId,
    bool Privileged,
    string? Decision, bool IsVerified, bool HasException, DateTimeOffset? ExceptionExpiresAt,
    Uuid? CurrentReviewerMemberId, Uuid? CurrentRemediationOwnerMemberId,
    bool HasProviderChange);
