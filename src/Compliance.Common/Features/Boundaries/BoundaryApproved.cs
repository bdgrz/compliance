using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.approved", 1)]
public sealed record BoundaryApproved(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateOnly EffectiveFrom,
    DateTimeOffset DecidedAt, string ImpactDigest) : DomainEvent;
