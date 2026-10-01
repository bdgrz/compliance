using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed record AccessReviewCampaignRegistration(Uuid CampaignId, Uuid SnapshotId,
    string ContentSha256, int ItemCount);
