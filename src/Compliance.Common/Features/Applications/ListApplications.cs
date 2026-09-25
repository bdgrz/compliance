using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.list", 1)]
public sealed record ListApplications(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationView>>, IApplicationInventoryRequest, ICallable;
