using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.version.effective.get", 1)]
public sealed record GetEffectiveBoundaryVersion(Uuid TenantId, Uuid BoundaryId,
    DateOnly EffectiveOn, long? MinimumBoundaryRevision = null)
    : IRequest<BoundaryVersionView>, ITenantAccessRequest, ICallable;
