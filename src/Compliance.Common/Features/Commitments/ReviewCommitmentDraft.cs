using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
///     An independent review that verifies owner, applicability, interpretation, and source
///     provenance. Acceptance does not make the commitment effective; a separate approval does.
///     HTTP-only: it is deliberately not an MCP tool.
/// </summary>
[Discriminator("bdgrz.commitment.draft.review", 1)]
public sealed record ReviewCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Outcome, string Rationale, string? OwnerReference = null,
    string? Applicability = null, string? Interpretation = null,
    string? InterpretationNote = null, string? SourceVerifiedReference = null,
    string? SourceEvidence = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
