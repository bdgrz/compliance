using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A personal approval that an in-scope system instance may lack an accepted population until
///     an expiry. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_population.exception.record", 1)]
public sealed record ExemptMissingAccessPopulation(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedLedgerRevision, string Reason, DateTimeOffset ExpiresAt)
    : IRequest<AccessPopulationExceptionView>, IAccessReviewMutationRequest, ICallable;
