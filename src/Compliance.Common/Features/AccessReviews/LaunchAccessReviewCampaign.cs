using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Launches a campaign over accepted populations, freezing its items, reviewers, instructions,
///     and deadline as an immutable snapshot.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.launch", 1)]
public sealed record LaunchAccessReviewCampaign(Uuid TenantId, string Name,
    string Instructions, DateTimeOffset Deadline,
    IReadOnlyList<AccessReviewAssignment> Assignments,
    Uuid? ProgramId = null, Uuid? RemediationOwnerMemberId = null)
    : IRequest<AccessReviewCampaignRegistration>, IAccessReviewMutationRequest, ICallable;
