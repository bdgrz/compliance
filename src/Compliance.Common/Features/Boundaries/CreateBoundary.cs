using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.create", 1)]
public sealed record CreateBoundary(Uuid TenantId, Uuid ProgramId, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, ICallable;
