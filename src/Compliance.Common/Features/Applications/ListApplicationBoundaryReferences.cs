using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved references, after source catchup.</summary>
public sealed record ListApplicationBoundaryReferences(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, IApplicationInventoryRequest, ICallable;
