using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

sealed class FitzTechnologyInventoryDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/technology-inventory-v1/projection",
            ProjectorName),
        ITechnologyInventoryReader, ITechnologyInventoryProjection
{
    public const string ProjectorName = "TechnologyInventoryDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            TechnologyInventoryStreams.TenantPattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TechnologyComponentRevisionRecorded ev:
                await ApplyAsync(TechnologyInventoryDirectorySchema.Components,
                    TechnologyInventoryDirectorySchema.ComponentRevisions, ev.ComponentId,
                    new TechnologyComponentView(ev.TenantId, ev.ComponentId, ev.Revision,
                        ev.Content, TechnologyInventoryStreams.ManualSource, ev.Actor,
                        ev.ChangedAt), static view => (view.TenantId, view.Revision), ct)
                    .ConfigureAwait(false);
                break;
            case InformationAssetRevisionRecorded ev:
                await ApplyAsync(TechnologyInventoryDirectorySchema.Assets,
                    TechnologyInventoryDirectorySchema.AssetRevisions, ev.InformationAssetId,
                    new InformationAssetView(ev.TenantId, ev.InformationAssetId, ev.Revision,
                        ev.Content, TechnologyInventoryStreams.ManualSource, ev.Actor,
                        ev.ChangedAt), static view => (view.TenantId, view.Revision), ct)
                    .ConfigureAwait(false);
                break;
            case DataFlowRevisionRecorded ev:
                await ApplyAsync(TechnologyInventoryDirectorySchema.Flows,
                    TechnologyInventoryDirectorySchema.FlowRevisions, ev.DataFlowId,
                    new DataFlowView(ev.TenantId, ev.DataFlowId, ev.Revision, ev.Content,
                        TechnologyInventoryStreams.ManualSource, ev.Actor, ev.ChangedAt),
                    static view => (view.TenantId, view.Revision), ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ApplyAsync<TView>(KvDirectory<TView, Uuid> current,
        KvDirectory<TView, string> revisions, Uuid id, TView next,
        Func<TView, (Uuid TenantId, long Revision)> identity, CancellationToken ct)
        where TView : class
    {
        var (tenantId, revision) = identity(next);
        if (revision == 1)
            await current.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
        {
            var existing = await current.GetAsync(Transaction, id, ct).ConfigureAwait(false);
            if (existing is null || identity(existing) != (tenantId, revision - 1))
                throw new InvalidOperationException(
                    "An inventory revision cannot project before its predecessor.");
            await current.ReplaceAsync(Transaction, existing, next, ct).ConfigureAwait(false);
        }
        await revisions.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
    }

    public ValueTask<TechnologyComponentView?> GetComponentAsync(Uuid tenantId,
        Uuid componentId, CancellationToken ct = default) =>
        GetAsync(tenantId, TechnologyInventoryDirectorySchema.Components, componentId, ct);

    public ValueTask<Page<TechnologyComponentView>> ListComponentsAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.Components,
            TechnologyInventoryDirectorySchema.ComponentsByName.Query(), limit, cursor, ct);

    public ValueTask<Page<TechnologyComponentView>> ListComponentRevisionsAsync(Uuid tenantId,
        Uuid componentId, int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.ComponentRevisions,
            TechnologyInventoryDirectorySchema.ComponentRevisionsById.Query()
                .WithPrefix(componentId.ToString()), limit, cursor, ct);

    public ValueTask<InformationAssetView?> GetAssetAsync(Uuid tenantId, Uuid assetId,
        CancellationToken ct = default) =>
        GetAsync(tenantId, TechnologyInventoryDirectorySchema.Assets, assetId, ct);

    public ValueTask<Page<InformationAssetView>> ListAssetsAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.Assets,
            TechnologyInventoryDirectorySchema.AssetsByName.Query(), limit, cursor, ct);

    public ValueTask<Page<InformationAssetView>> ListAssetRevisionsAsync(Uuid tenantId,
        Uuid assetId, int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.AssetRevisions,
            TechnologyInventoryDirectorySchema.AssetRevisionsById.Query()
                .WithPrefix(assetId.ToString()), limit, cursor, ct);

    public ValueTask<DataFlowView?> GetFlowAsync(Uuid tenantId, Uuid flowId,
        CancellationToken ct = default) =>
        GetAsync(tenantId, TechnologyInventoryDirectorySchema.Flows, flowId, ct);

    public ValueTask<Page<DataFlowView>> ListFlowsAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.Flows,
            TechnologyInventoryDirectorySchema.FlowsByPurpose.Query(), limit, cursor, ct);

    public ValueTask<Page<DataFlowView>> ListFlowRevisionsAsync(Uuid tenantId, Uuid flowId,
        int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, TechnologyInventoryDirectorySchema.FlowRevisions,
            TechnologyInventoryDirectorySchema.FlowRevisionsById.Query()
                .WithPrefix(flowId.ToString()), limit, cursor, ct);

    async ValueTask<TView?> GetAsync<TView>(Uuid tenantId, KvDirectory<TView, Uuid> directory,
        Uuid id, CancellationToken ct) where TView : class
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await directory.GetAsync(tx, id, ct).ConfigureAwait(false);
    }

    async ValueTask<Page<TView>> QueryAsync<TView, TKey>(Uuid tenantId,
        KvDirectory<TView, TKey> directory, KvDirectoryQuery<TView> query, int limit,
        string? cursor, CancellationToken ct) where TView : class
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await directory.QueryAsync(tx, query.Take(Math.Clamp(limit, 1, 200))
            .After(cursor), ct).ConfigureAwait(false);
    }
}
