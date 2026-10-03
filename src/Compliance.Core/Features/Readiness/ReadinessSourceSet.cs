using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Providers;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The program's canonical source records, with their histories, read once for one readiness
///     run. The rules resolve each record as of the assessment time; readiness never stores a
///     second copy of source state. A family whose scan hit the read limit is listed as truncated.
/// </summary>
public sealed record ReadinessSourceSet(IReadOnlyList<ReadinessBoundaryInput> Boundaries,
    IReadOnlyList<ReadinessCommitmentInput> Commitments, IReadOnlyList<ReadinessRiskInput> Risks,
    IReadOnlyList<SnapshotView> Snapshots)
{
    public static ReadinessSourceSet Empty { get; } = new([], [], [], []);

    public IReadOnlyList<ReadinessAccessReviewScopeInput> AccessReviewScopes { get; init; } = [];

    public IReadOnlyList<ReadinessProviderInput> Providers { get; init; } = [];

    public IReadOnlyList<string> TruncatedFamilies { get; init; } = [];
}
