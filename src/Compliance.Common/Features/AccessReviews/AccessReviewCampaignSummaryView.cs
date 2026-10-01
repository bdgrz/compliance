using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A campaign's identity and status, without its items.</summary>
public sealed record AccessReviewCampaignSummaryView(Uuid TenantId, Uuid CampaignId,
    string Name, DateTimeOffset Deadline, string Status, int ItemCount, Uuid SnapshotId,
    DateTimeOffset LaunchedAt, Uuid? FinalSnapshotId, DateTimeOffset? CompletedAt);
