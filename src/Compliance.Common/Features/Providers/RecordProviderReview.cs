using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Records the acting member's personal due-diligence conclusion. HTTP-only: it is deliberately
///     not an MCP tool, so a machine cannot sign a review.
/// </summary>
[Discriminator("bdgrz.provider.review.record", 1)]
public sealed record RecordProviderReview(Uuid TenantId, Uuid ProviderId, ProviderReviewContent Content)
    : IRequest<ProviderReviewRegistration>, IProviderManagementRequest, ICallable;
