using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.operational_process.revision.list", 1)]
public sealed record ListOperationalProcessRevisions(Uuid TenantId, Uuid OperationalProcessId, int? Limit = null,
    string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<OperationalProcessView>>, ITechnologyInventoryRequest, ICallable;
