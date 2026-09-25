using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.get", 1)]
public sealed record GetBoundary(Uuid TenantId, Uuid BoundaryId, long? MinimumRevision = null)
    : IRequest<BoundaryView>, ITenantAccessRequest, ICallable;
