using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The completion attestation and the final immutable campaign snapshot.</summary>
public sealed record AccessReviewCampaignCompletionView(Uuid SnapshotId, string ContentSha256,
    string Attestation, ActorReference CompletedBy, DateTimeOffset CompletedAt);
