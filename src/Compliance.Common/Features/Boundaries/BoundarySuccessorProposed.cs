using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.successor.proposed", 1)]
public sealed record BoundarySuccessorProposed(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, Uuid PredecessorVersionId, BoundaryContent Content,
    Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
