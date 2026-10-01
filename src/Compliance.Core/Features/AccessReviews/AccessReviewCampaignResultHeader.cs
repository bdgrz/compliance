using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Links a final campaign snapshot to the launch snapshot it completes.</summary>
public sealed record AccessReviewCampaignResultHeader(Uuid CampaignId, string Name,
    DateTimeOffset Deadline, Uuid LaunchSnapshotId, string LaunchContentSha256,
    string Attestation);
