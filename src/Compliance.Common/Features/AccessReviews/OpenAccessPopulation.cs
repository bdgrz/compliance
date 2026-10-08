using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Opens a manually attested population draft for one exact system-instance revision.</summary>
[Discriminator("bdgrz.access_population.open", 1)]
public sealed record OpenAccessPopulation(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedSystemInstanceRevision, DateTimeOffset ObservedAt,
    string Source)
    : IRequest<AccessPopulationRegistration>, IAccessReviewMutationRequest, ICallable;
