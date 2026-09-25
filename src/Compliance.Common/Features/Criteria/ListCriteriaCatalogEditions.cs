using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

[Discriminator("bdgrz.criteria.editions.list", 1)]
public sealed record ListCriteriaCatalogEditions(Uuid TenantId)
    : IRequest<IReadOnlyList<CriteriaCatalogEdition>>, ITenantAccessRequest, ICallable;
