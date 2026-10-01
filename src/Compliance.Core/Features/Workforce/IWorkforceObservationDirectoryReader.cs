using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IWorkforceObservationDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<Page<WorkforceObservationView>> ListAsync(Uuid tenantId, string? kind, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<WorkforceObservationView?> GetAsync(Uuid tenantId, Uuid observationId,
        CancellationToken ct = default);
    /// <summary>Returns the lifecycle status of every recorded relationship of one person.</summary>
    ValueTask<IReadOnlyList<string>> ListRelationshipStatusesAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default);
}
