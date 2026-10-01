using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
///     Approves the latest accepted review of the exact draft revision into an immutable effective
///     version. HTTP-only: it is deliberately not an MCP tool.
/// </summary>
[Discriminator("bdgrz.commitment.draft.approve", 1)]
public sealed record ApproveCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom,
    string Rationale, string ImpactDigest, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, ICallable;
