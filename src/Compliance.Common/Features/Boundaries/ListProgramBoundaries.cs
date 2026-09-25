using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.program.list", 1)]
public sealed record ListProgramBoundaries(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryView>>, ITenantAccessRequest, ICallable;
