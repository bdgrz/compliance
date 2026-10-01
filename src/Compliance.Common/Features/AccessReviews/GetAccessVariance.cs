using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Explains an accepted population against approved expectations without deciding any item.</summary>
[Discriminator("bdgrz.access_population.variance.get", 1)]
public sealed record GetAccessVariance(Uuid TenantId, Uuid PopulationId)
    : IRequest<AccessVarianceView>, IAccessReviewRequest, ICallable;
