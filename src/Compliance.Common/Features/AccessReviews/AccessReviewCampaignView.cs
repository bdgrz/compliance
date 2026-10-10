using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A launched campaign. Its population, reviewers, instructions, and deadline are frozen in
///     <c>SnapshotId</c>. <c>Status</c> is <c>active</c> or <c>completed</c>.
/// </summary>
public sealed record AccessReviewCampaignView(Uuid TenantId, Uuid CampaignId, long Revision,
    string Name, string Instructions, DateTimeOffset Deadline, string Status, Uuid SnapshotId,
    string ContentSha256, IReadOnlyList<AccessReviewerView> Reviewers,
    IReadOnlyList<AccessReviewItemStateView> Items, int UnresolvedCount,
    int RemediationOpenCount, ActorReference LaunchedBy, DateTimeOffset LaunchedAt,
    AccessReviewCampaignCompletionView? Completion,
    Uuid? ProgramId = null, Uuid? RemediationOwnerMemberId = null);
