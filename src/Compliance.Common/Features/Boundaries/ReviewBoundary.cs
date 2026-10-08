using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.review", 1)]
public sealed record ReviewBoundary(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, string Outcome, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, ICallable;
