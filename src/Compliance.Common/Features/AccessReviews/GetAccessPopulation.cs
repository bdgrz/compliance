using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads a population draft or its accepted immutable snapshot.</summary>
[Discriminator("bdgrz.access_population.get", 1)]
public sealed record GetAccessPopulation(Uuid TenantId, Uuid PopulationId)
    : IRequest<AccessPopulationView>, IAccessReviewRequest, ICallable;
