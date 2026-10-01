using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Lists one system instance's populations, newest observation first.</summary>
[Discriminator("bdgrz.access_population.list", 1)]
public sealed record ListAccessPopulations(Uuid TenantId, Uuid SystemInstanceId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<AccessPopulationSummaryView>>, IAccessReviewRequest, ICallable;
