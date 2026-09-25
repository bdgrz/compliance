using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.list", 1)]
public sealed record ListClientServices(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ClientServiceView>>, ITenantAccessRequest, ICallable;
