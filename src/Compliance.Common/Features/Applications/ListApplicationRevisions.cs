using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.revision.list", 1)]
public sealed record ListApplicationRevisions(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumApplicationRevision = null)
    : IRequest<Page<ApplicationRevisionView>>, IApplicationInventoryRequest, ICallable;
