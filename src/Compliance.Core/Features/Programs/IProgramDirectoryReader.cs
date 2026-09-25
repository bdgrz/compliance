using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProgramDirectoryReader
{
    ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default);
    ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
        long revision, CancellationToken ct = default);
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}
