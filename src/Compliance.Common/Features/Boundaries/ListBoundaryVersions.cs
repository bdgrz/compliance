using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.versions.list", 1)]
public sealed record ListBoundaryVersions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null, long? MinimumBoundaryRevision = null)
    : IRequest<Page<BoundaryVersionView>>, ITenantAccessRequest, ICallable;
