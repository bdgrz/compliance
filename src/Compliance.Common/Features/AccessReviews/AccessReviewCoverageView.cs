using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The access-population coverage of every system instance of one application.</summary>
public sealed record AccessReviewCoverageView(Uuid TenantId, Uuid ApplicationId,
    DateTimeOffset AsOf, IReadOnlyList<AccessReviewInstanceCoverageView> Instances);
