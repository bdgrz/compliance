using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface IInventoryRegisterReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<LocationView?> GetLocationAsync(Uuid tenantId, Uuid id,
        CancellationToken ct = default);
    ValueTask<Page<LocationView>> ListLocationsAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<Page<LocationView>> ListLocationRevisionsAsync(Uuid tenantId, Uuid id, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<OperationalProcessView?> GetOperationalProcessAsync(Uuid tenantId, Uuid id,
        CancellationToken ct = default);
    ValueTask<Page<OperationalProcessView>> ListOperationalProcessesAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<Page<OperationalProcessView>> ListOperationalProcessRevisionsAsync(Uuid tenantId,
        Uuid id, int limit, string? cursor, CancellationToken ct = default);
}
