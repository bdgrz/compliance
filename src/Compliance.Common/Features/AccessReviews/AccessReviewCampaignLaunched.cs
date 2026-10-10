using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A campaign was launched with its frozen population, reviewers, instructions, and deadline.</summary>
[Discriminator("bdgrz.access_review.campaign.launched", 1)]
public sealed record AccessReviewCampaignLaunched(Uuid TenantId,
    Uuid CampaignId, string Name, string Instructions, DateTimeOffset Deadline, Uuid SnapshotId,
    string ContentSha256, IReadOnlyList<AccessReviewerView> Reviewers,
    IReadOnlyList<AccessReviewItemView> Items, ActorReference LaunchedBy, DateTimeOffset LaunchedAt,
    Uuid? ProgramId = null, Uuid? RemediationOwnerMemberId = null) : DomainEvent;
