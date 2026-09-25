using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.successor.propose", 1)]
public sealed record ProposeBoundarySuccessor(Uuid TenantId, Uuid BoundaryId,
    Uuid ExpectedApprovedVersionId, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, ICallable;
