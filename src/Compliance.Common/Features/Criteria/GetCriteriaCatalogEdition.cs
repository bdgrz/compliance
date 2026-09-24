using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

[Discriminator("bdgrz.criteria.edition.get", 1)]
public sealed record GetCriteriaCatalogEdition(Uuid TenantId, Uuid EditionId)
    : IRequest<CriteriaCatalogEdition>, ITenantAccessRequest, ICallable;
