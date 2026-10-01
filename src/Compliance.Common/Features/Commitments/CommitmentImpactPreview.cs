using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
/// Changes between the latest effective version and the draft revision, plus dependents
/// found in linked contexts (boundaries and controls). <c>UnlinkedContexts</c> names contexts
/// that cannot reference commitments and why, so they contribute no dependents.
/// </summary>
public sealed record CommitmentImpactPreview(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, long? EffectiveVersion, IReadOnlyList<CommitmentChange> Changes,
    IReadOnlyList<CommitmentDependent> Dependents, IReadOnlyList<CommitmentUnlinkedContext> UnlinkedContexts,
    bool Complete, string Digest);
