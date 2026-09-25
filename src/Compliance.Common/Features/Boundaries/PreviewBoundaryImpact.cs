using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.impact.preview", 1)]
public sealed record PreviewBoundaryImpact(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision)
    : IRequest<BoundaryImpactPreview>, ITenantAccessRequest, ICallable;
