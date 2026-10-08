using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A personal approval that a required remediation may remain unverified. The item's reviewer
///     cannot approve it. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.remediation.exception.record", 1)]
public sealed record ExemptAccessRemediation(Uuid TenantId, Uuid CampaignId,
    Uuid ItemId, long ExpectedRevision, string Rationale, DateTimeOffset? ExpiresAt = null)
    : IRequest<AccessRemediationExceptionView>, IAccessReviewMutationRequest, ICallable;
