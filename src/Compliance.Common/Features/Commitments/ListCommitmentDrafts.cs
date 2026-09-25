using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.list", 1)]
public sealed record ListCommitmentDrafts(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentDraftView>>, IProgramManagementRequest, ICallable;
