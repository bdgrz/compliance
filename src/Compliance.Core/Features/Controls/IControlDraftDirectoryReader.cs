using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlDraftDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
        CancellationToken ct = default);
    ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId,
        long revision, CancellationToken ct = default);
}
