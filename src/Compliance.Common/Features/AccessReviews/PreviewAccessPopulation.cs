using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Shows invalid, duplicate, incomplete, and ambiguous facts and the derived effective access before acceptance.</summary>
[Discriminator("bdgrz.access_population.preview", 1)]
public sealed record PreviewAccessPopulation(Uuid TenantId, Uuid PopulationId)
    : IRequest<AccessPopulationPreview>, IAccessReviewRequest, ICallable;
