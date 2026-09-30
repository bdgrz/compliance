using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>
/// Changes between the latest effective version and the draft revision, plus dependents
/// found in linked contexts. <c>UnlinkedContexts</c> names contexts that cannot yet
/// reference commitments, so they contribute no dependents.
/// </summary>
public sealed record CommitmentImpactPreview(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, long? EffectiveVersion, IReadOnlyList<CommitmentChange> Changes,
    IReadOnlyList<CommitmentDependent> Dependents, IReadOnlyList<string> UnlinkedContexts,
    bool Complete, string Digest);
