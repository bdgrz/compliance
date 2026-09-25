using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.revision.get", 1)]
public sealed record GetCommitmentDraftRevision(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, long Revision)
    : IRequest<CommitmentDraftRevisionView>, IProgramManagementRequest, ICallable;
