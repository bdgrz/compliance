using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.location.revision.list", 1)]
public sealed record ListLocationRevisions(Uuid TenantId, Uuid LocationId, int? Limit = null,
    string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<LocationView>>, ITechnologyInventoryRequest, ICallable;
