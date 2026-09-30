using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.impact.preview", 1)]
public sealed record PreviewCommitmentImpact(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision)
    : IRequest<CommitmentImpactPreview>, IProgramScopedRequest, ICallable;
