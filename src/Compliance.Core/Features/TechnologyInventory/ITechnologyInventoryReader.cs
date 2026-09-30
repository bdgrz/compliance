using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface ITechnologyInventoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<TechnologyComponentView?> GetComponentAsync(Uuid tenantId, Uuid componentId,
        CancellationToken ct = default);
    ValueTask<Page<TechnologyComponentView>> ListComponentsAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<Page<TechnologyComponentView>> ListComponentRevisionsAsync(Uuid tenantId,
        Uuid componentId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<InformationAssetView?> GetAssetAsync(Uuid tenantId, Uuid assetId,
        CancellationToken ct = default);
    ValueTask<Page<InformationAssetView>> ListAssetsAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<Page<InformationAssetView>> ListAssetRevisionsAsync(Uuid tenantId, Uuid assetId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<DataFlowView?> GetFlowAsync(Uuid tenantId, Uuid flowId,
        CancellationToken ct = default);
    ValueTask<Page<DataFlowView>> ListFlowsAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
    ValueTask<Page<DataFlowView>> ListFlowRevisionsAsync(Uuid tenantId, Uuid flowId,
        int limit, string? cursor, CancellationToken ct = default);
}
