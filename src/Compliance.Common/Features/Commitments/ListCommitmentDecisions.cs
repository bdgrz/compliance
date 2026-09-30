using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.decision.list", 1)]
public sealed record ListCommitmentDecisions(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentDecisionView>>, IProgramScopedRequest, ICallable;
