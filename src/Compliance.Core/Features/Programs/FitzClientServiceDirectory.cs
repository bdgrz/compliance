using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class FitzClientServiceDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/client-service-directory/projection", "ClientServiceDirectory"),
        IClientServiceDirectoryReader, IClientServiceDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ClientServiceCreated created:
                await ClientServiceDirectorySchema.Directory.InsertAsync(Transaction,
                    new ClientServiceView(created.TenantId, created.ServiceId, 1, created.Name,
                        created.Purpose, created.OwnerReference, "active", created.ActorMemberId,
                        created.ActorDisplay, created.ChangedAt,
                        created.ProgramId == Uuid.Empty ? null : created.ProgramId), ct).ConfigureAwait(false);
                await ClientServiceDirectorySchema.Revisions.InsertAsync(Transaction,
                        new ClientServiceRevisionView(created.ServiceId, 1, created.Name,
                            created.Purpose, created.OwnerReference, "active", null,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt,
                            created.ProgramId == Uuid.Empty ? null : created.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
            case ClientServiceRevised revised:
                var current = await RequireCurrentAsync(revised.ServiceId, ct).ConfigureAwait(false);
                await ClientServiceDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        Name = revised.Name,
                        Purpose = revised.Purpose,
                        OwnerReference = revised.OwnerReference,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ClientServiceDirectorySchema.Revisions.InsertAsync(Transaction,
                        new ClientServiceRevisionView(revised.ServiceId, revised.Revision, revised.Name,
                            revised.Purpose, revised.OwnerReference, "active", null,
                            revised.ActorMemberId, revised.ActorDisplay, revised.ChangedAt,
                            current.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
            case ClientServiceRetired retired:
                var active = await RequireCurrentAsync(retired.ServiceId, ct).ConfigureAwait(false);
                await ClientServiceDirectorySchema.Directory.ReplaceAsync(Transaction, active,
                    active with
                    {
                        Revision = retired.Revision,
                        Status = "retired",
                        LastChangedByMemberId = retired.ActorMemberId,
                        LastChangedByDisplay = retired.ActorDisplay,
                        LastChangedAt = retired.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await ClientServiceDirectorySchema.Revisions.InsertAsync(Transaction,
                        new ClientServiceRevisionView(retired.ServiceId, retired.Revision, active.Name,
                            active.Purpose, active.OwnerReference, "retired", retired.Rationale,
                            retired.ActorMemberId, retired.ActorDisplay, retired.ChangedAt,
                            active.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    async ValueTask<ClientServiceView> RequireCurrentAsync(Uuid serviceId, CancellationToken ct) =>
        await ClientServiceDirectorySchema.Directory.GetAsync(Transaction, serviceId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A service change cannot project before its creation.");

    public async ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ClientServiceDirectorySchema.Directory.GetAsync(tx, serviceId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ClientServiceDirectorySchema.Directory.QueryAsync(tx,
                ClientServiceDirectorySchema.ByName.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ClientServiceDirectorySchema.Directory.QueryAsync(tx,
            ClientServiceDirectorySchema.ByProgram.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid serviceId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await ClientServiceDirectorySchema.Directory.GetAsync(tx, serviceId, ct)
                .ConfigureAwait(false) is null)
            return null;
        return await ClientServiceDirectorySchema.Revisions.QueryAsync(tx,
            ClientServiceDirectorySchema.RevisionsByService.Query().WithPrefix(serviceId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<ClientServiceRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid serviceId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ClientServiceDirectorySchema.Revisions.GetAsync(tx,
                $"{serviceId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}", ct)
            .ConfigureAwait(false);
    }
}
