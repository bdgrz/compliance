using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The items a bulk decision would record and the items it cannot. A bulk decision must name
///     exactly the eligible items and present <c>PreviewToken</c>.
/// </summary>
public sealed record BulkAccessDecisionPreview(Uuid CampaignId, long Revision, string Decision,
    IReadOnlyList<AccessReviewItemView> Eligible, IReadOnlyList<BulkAccessDecisionRejection> Rejected,
    string PreviewToken);
