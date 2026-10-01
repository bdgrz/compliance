using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The assigned reviewer's personal decision on one item. A reviewer may not decide their own
///     access without an exact-scope waiver. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.decision.record", 1)]
public sealed record RecordAccessDecision(Uuid TenantId, Uuid CampaignId, Uuid ItemId,
    long ExpectedRevision, string Decision, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<AccessDecisionView>, IAccessReviewParticipantRequest, ICallable;
