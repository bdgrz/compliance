using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

[Discriminator("bdgrz.inventory.operational_process.list", 1)]
public sealed record ListOperationalProcesses(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<OperationalProcessView>>, ITechnologyInventoryRequest, ICallable;
