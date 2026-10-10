using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The frozen instructions and deadline of a launched campaign.</summary>
public sealed record AccessReviewCampaignHeader(Uuid CampaignId, string Name, string Instructions,
    DateTimeOffset Deadline, Uuid? ProgramId = null, Uuid? RemediationOwnerMemberId = null);
