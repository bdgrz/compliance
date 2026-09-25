using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.version.get", 1)]
public sealed record GetBoundaryVersion(Uuid TenantId, Uuid BoundaryId, Uuid VersionId,
    long? MinimumBoundaryRevision = null)
    : IRequest<BoundaryVersionView>, ITenantAccessRequest, ICallable;
