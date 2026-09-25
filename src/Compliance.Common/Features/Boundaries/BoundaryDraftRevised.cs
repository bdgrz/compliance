using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.revised", 1)]
public sealed record BoundaryDraftRevised(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
