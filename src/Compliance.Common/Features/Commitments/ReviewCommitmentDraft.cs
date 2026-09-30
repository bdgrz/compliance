using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>An independent review. HTTP-only: it is deliberately not an MCP tool.</summary>
[Discriminator("bdgrz.commitment.draft.review", 1)]
public sealed record ReviewCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Outcome, string Rationale, string? OwnerReference = null,
    string? Applicability = null, string? Interpretation = null,
    string? InterpretationNote = null, DateOnly? EffectiveFrom = null,
    string? ImpactDigest = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, ICallable;
