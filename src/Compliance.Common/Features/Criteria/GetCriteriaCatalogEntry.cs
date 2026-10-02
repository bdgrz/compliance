using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

[Discriminator("bdgrz.criteria.entry.get", 1)]
public sealed record GetCriteriaCatalogEntry(Uuid TenantId, Uuid EditionId,
    string Identifier, string? Purpose = null) : IRequest<Criterion>, ITenantAccessRequest, ICallable;
