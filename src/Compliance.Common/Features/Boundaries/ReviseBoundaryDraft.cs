using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.draft.revise", 1)]
public sealed record ReviseBoundaryDraft(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, BoundaryContent Content)
    : IRequest, IBoundaryAuthoringRequest, ICallable;
