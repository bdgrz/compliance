using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.decisions.list", 1)]
public sealed record ListBoundaryDecisions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryDecisionView>>, ITenantAccessRequest, ICallable;
