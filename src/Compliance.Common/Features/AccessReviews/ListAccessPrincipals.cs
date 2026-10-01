using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Lists an accepted population's principals with classification, proposals, and open gaps.</summary>
[Discriminator("bdgrz.access_population.principals.list", 1)]
public sealed record ListAccessPrincipals(Uuid TenantId, Uuid PopulationId,
    string? Gap = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AccessPrincipalView>>, IAccessReviewRequest, ICallable;
