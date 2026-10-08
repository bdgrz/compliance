using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The reviewer's personal attestation that the draft is the observed population. It freezes
///     an immutable snapshot. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_population.accept", 1)]
public sealed record AcceptAccessPopulation(Uuid TenantId, Uuid PopulationId,
    long ExpectedRevision, string Attestation)
    : IRequest<AccessPopulationAcceptance>, IAccessReviewMutationRequest, ICallable;
