using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.created", 1)]
public sealed record BoundaryDraftCreated(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid DraftVersionId, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
