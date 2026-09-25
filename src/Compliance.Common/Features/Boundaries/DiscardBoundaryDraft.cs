using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.discard", 1)]
public sealed record DiscardBoundaryDraft(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision, string Rationale)
    : IRequest, IBoundaryAuthoringRequest, ICallable;
