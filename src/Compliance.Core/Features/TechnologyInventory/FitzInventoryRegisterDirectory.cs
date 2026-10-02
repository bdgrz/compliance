using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

sealed class FitzInventoryRegisterDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/inventory-registers-v1/projection",
            ProjectorName),
        IInventoryRegisterReader, IInventoryRegisterProjection
{
    public const string ProjectorName = "InventoryRegistersV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            InventoryRegisters.TenantPattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case LocationRevisionRecorded ev:
                await ApplyAsync(InventoryRegisterDirectorySchema.Locations,
                    InventoryRegisterDirectorySchema.LocationRevisions,
                    ComplianceCoreJsonContext.Default.LocationView, ev.LocationId,
                    new LocationView(ev.TenantId, ev.LocationId, ev.Revision, ev.Content,
                        InventoryRegisters.ManualSource, ev.Actor, ev.ChangedAt),
                    static view => (view.TenantId, view.Revision), ct).ConfigureAwait(false);
                break;
            case OperationalProcessRevisionRecorded ev:
                await ApplyAsync(InventoryRegisterDirectorySchema.Processes,
                    InventoryRegisterDirectorySchema.ProcessRevisions,
                    ComplianceCoreJsonContext.Default.OperationalProcessView,
                    ev.OperationalProcessId,
                    new OperationalProcessView(ev.TenantId, ev.OperationalProcessId,
                        ev.Revision, ev.Content, InventoryRegisters.ManualSource, ev.Actor,
                        ev.ChangedAt), static view => (view.TenantId, view.Revision), ct)
                    .ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("The inventory register event is not supported.");
        }
    }

    // Replaying an already-projected revision is a no-op; changed content never replaces it.
    async ValueTask ApplyAsync<TView>(KvDirectory<TView, Uuid> current,
        KvDirectory<TView, string> revisions, JsonTypeInfo<TView> json, Uuid id, TView next,
        Func<TView, (Uuid TenantId, long Revision)> identity, CancellationToken ct)
        where TView : class
    {
        var (tenantId, revision) = identity(next);
        if (tenantId == Uuid.Empty || id == Uuid.Empty || revision < 1)
            throw new InvalidOperationException("An inventory register revision requires its identities.");
        var retained = await revisions.GetAsync(Transaction,
            InventoryRegisterDirectorySchema.RevisionKey(id, revision), ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!JsonSerializer.SerializeToUtf8Bytes(retained, json).AsSpan()
                    .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(next, json)))
                throw new InvalidOperationException(
                    "An inventory register revision cannot replace retained content.");
            return;
        }
        var existing = await current.GetAsync(Transaction, id, ct).ConfigureAwait(false);
        if (revision == 1 ? existing is not null :
                existing is null || identity(existing) != (tenantId, revision - 1))
            throw new InvalidOperationException(
                "An inventory register revision requires its tenant's immediate predecessor.");
        if (existing is null)
            await current.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
            await current.ReplaceAsync(Transaction, existing, next, ct).ConfigureAwait(false);
        await revisions.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
    }

    public ValueTask<LocationView?> GetLocationAsync(Uuid tenantId, Uuid id,
        CancellationToken ct = default) =>
        GetAsync(tenantId, InventoryRegisterDirectorySchema.Locations, id, ct);

    public ValueTask<Page<LocationView>> ListLocationsAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, InventoryRegisterDirectorySchema.Locations,
            InventoryRegisterDirectorySchema.LocationsByName.Query(), limit, cursor, ct);

    public ValueTask<Page<LocationView>> ListLocationRevisionsAsync(Uuid tenantId, Uuid id,
        int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, InventoryRegisterDirectorySchema.LocationRevisions,
            InventoryRegisterDirectorySchema.LocationRevisionsById.Query()
                .WithPrefix(id.ToString()), limit, cursor, ct);

    public ValueTask<OperationalProcessView?> GetOperationalProcessAsync(Uuid tenantId, Uuid id,
        CancellationToken ct = default) =>
        GetAsync(tenantId, InventoryRegisterDirectorySchema.Processes, id, ct);

    public ValueTask<Page<OperationalProcessView>> ListOperationalProcessesAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, InventoryRegisterDirectorySchema.Processes,
            InventoryRegisterDirectorySchema.ProcessesByName.Query(), limit, cursor, ct);

    public ValueTask<Page<OperationalProcessView>> ListOperationalProcessRevisionsAsync(
        Uuid tenantId, Uuid id, int limit, string? cursor, CancellationToken ct = default) =>
        QueryAsync(tenantId, InventoryRegisterDirectorySchema.ProcessRevisions,
            InventoryRegisterDirectorySchema.ProcessRevisionsById.Query()
                .WithPrefix(id.ToString()), limit, cursor, ct);

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
