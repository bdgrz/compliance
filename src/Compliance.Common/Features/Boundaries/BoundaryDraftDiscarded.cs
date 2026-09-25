using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.discarded", 1)]
public sealed record BoundaryDraftDiscarded(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DiscardedAt) : DomainEvent;
