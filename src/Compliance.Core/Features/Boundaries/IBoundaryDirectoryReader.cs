using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryDirectoryReader
{
    ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
        CancellationToken ct = default);
    ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
        Uuid versionId, CancellationToken ct = default);
    ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId, Uuid boundaryId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
        Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default);
    ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
        Uuid boundaryId, Uuid decisionId, CancellationToken ct = default);
    ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
        Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
