using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskDraftDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
        CancellationToken ct = default);
    ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
        long revision, CancellationToken ct = default);
}
