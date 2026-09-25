using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.client-service.revisions.list", 1)]
public sealed record ListClientServiceRevisions(Uuid TenantId, Uuid ServiceId,
    int? Limit = null, string? Cursor = null, long? MinimumServiceRevision = null)
    : IRequest<Page<ClientServiceRevisionView>>, ITenantAccessRequest, ICallable;
