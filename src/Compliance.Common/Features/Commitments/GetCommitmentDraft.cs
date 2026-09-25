using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.get", 1)]
public sealed record GetCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long? MinimumRevision = null)
    : IRequest<CommitmentDraftView>, IProgramManagementRequest, ICallable;
