using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The program's canonical source records read once for one readiness run. Readiness is
///     derived from these records; it never stores a second copy of their state.
/// </summary>
public sealed record ReadinessSourceSet(IReadOnlyList<BoundaryView> Boundaries,
    IReadOnlyList<CommitmentDraftView> Commitments, IReadOnlyList<ReadinessRiskInput> Risks,
    IReadOnlyList<SnapshotView> Snapshots)
{
    public static ReadinessSourceSet Empty { get; } = new([], [], [], []);
}
