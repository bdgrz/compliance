using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.revise", 1)]
public sealed record ReviseCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Statement, string Context, string SourceReference)
    : IRequest, IProgramManagementRequest, ICallable;
