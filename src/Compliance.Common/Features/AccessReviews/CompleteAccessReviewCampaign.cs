using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The owner's personal completion sign-off. It freezes the final campaign snapshot and is
///     refused while any item is unresolved or any required remediation is unverified without an
///     approved exception. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.campaign.complete", 1)]
public sealed record CompleteAccessReviewCampaign(Uuid TenantId, Uuid CampaignId,
    long ExpectedRevision, string Attestation)
    : IRequest<AccessReviewCampaignCompletionView>, IAccessReviewMutationRequest, ICallable;
