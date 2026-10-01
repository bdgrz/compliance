using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reconciles each in-scope system instance to an accepted population or an approved exception.</summary>
[Discriminator("bdgrz.access_review.coverage.get", 1)]
public sealed record GetAccessReviewCoverage(Uuid TenantId, Uuid ApplicationId, DateTimeOffset? AsOf = null)
    : IRequest<AccessReviewCoverageView>, IAccessReviewRequest, ICallable;
