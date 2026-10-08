using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Explicitly classifies an accepted account; a new decision supersedes and preserves the prior one.</summary>
[Discriminator("bdgrz.access_population.principal.classify", 1)]
public sealed record ClassifyAccessPrincipal(Uuid TenantId, Uuid PopulationId,
    string ProviderSubjectId, long ExpectedClassificationCount, string Classification,
    string Rationale, Uuid? PersonId = null, Uuid? ServiceIdentityId = null,
    Uuid? AccountableOwnerPersonId = null, string? SharedJustification = null)
    : IRequest<AccessPrincipalClassificationView>, IAccessReviewMutationRequest, ICallable;
