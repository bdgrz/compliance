using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An explicit classification decision; earlier decisions remain in history.</summary>
[Discriminator("bdgrz.access_population.principal_classified", 1)]
public sealed record AccessPrincipalClassified(Uuid TenantId,
    Uuid PopulationId, string ProviderSubjectId, AccessPrincipalClassificationView Decision) : DomainEvent;
