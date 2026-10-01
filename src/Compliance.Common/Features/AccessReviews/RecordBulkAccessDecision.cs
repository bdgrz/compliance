using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records one shared-rationale decision on exactly the previewed eligible items. HTTP-only;
///     it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_review.decision.bulk_record", 1)]
public sealed record RecordBulkAccessDecision(Uuid TenantId, Uuid CampaignId,
    IReadOnlyList<Uuid> ItemIds, string Decision, string Rationale, string PreviewToken)
    : IRequest<BulkAccessDecisionResult>, IAccessReviewParticipantRequest, ICallable;
