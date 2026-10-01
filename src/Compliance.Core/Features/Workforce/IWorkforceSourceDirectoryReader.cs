using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceSourceDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<WorkforceSourceView?> GetAsync(Uuid tenantId, Uuid observationId, CancellationToken ct = default);
    ValueTask<Page<WorkforceSourceView>> ListAsync(Uuid tenantId, string? targetKind, Uuid? targetId,
        int limit, string? cursor, CancellationToken ct = default);
}
