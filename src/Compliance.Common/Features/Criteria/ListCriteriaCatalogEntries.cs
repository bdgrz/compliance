using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

[Discriminator("bdgrz.criteria.entries.list", 1)]
public sealed record ListCriteriaCatalogEntries(Uuid TenantId, Uuid EditionId,
    string? Category = null, string? Kind = null, string? ParentIdentifier = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<Criterion>>, ITenantAccessRequest, ICallable;
