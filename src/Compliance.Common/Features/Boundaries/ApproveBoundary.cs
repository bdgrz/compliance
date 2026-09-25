using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.approve", 1)]
public sealed record ApproveBoundary(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom,
    string Rationale, string ImpactDigest) : IRequest, IBoundaryAuthoringRequest, ICallable;
