using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.reviewed", 1)]
public sealed record BoundaryReviewed(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid DecisionId, string Outcome, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DecidedAt) : DomainEvent;
