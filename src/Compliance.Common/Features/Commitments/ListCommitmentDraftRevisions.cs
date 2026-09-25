using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.revision.list", 1)]
public sealed record ListCommitmentDraftRevisions(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, int? Limit = null, string? Cursor = null,
    long? MinimumDraftRevision = null)
    : IRequest<Page<CommitmentDraftRevisionView>>, IProgramManagementRequest, ICallable;
