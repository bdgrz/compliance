using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.list", 1)]
public sealed record ListSystemInstances(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumApplicationRevision = null)
    : IRequest<Page<SystemInstanceView>>, IApplicationInventoryRequest, ICallable;
