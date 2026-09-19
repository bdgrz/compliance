using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProgramDirectoryReader
{
    ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default);
    ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default);
}

public interface IProgramDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class ProgramDirectorySchema
{
    public static readonly KvDirectoryIndex<ProgramView> ByName = new(
        "by_name", 1, static program => [program.Name.ToUpperInvariant()]);

    public static readonly KvDirectory<ProgramView, Uuid> Directory = new(
        "programs", ComplianceCoreJsonContext.Default.ProgramView,
        static program => program.ProgramId,
        static programId => [programId.ToString()], [ByName]);
}

sealed class FitzProgramDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/program-directory/projection", "ProgramDirectory"),
      IProgramDirectoryReader, IProgramDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ProgramCreated created:
                await ProgramDirectorySchema.Directory.InsertAsync(Transaction,
                    new ProgramView(created.TenantId, created.ProgramId, created.Name,
                        "readiness", "type_i", 1, created.Plan,
                        created.ActorMemberId, created.ActorDisplay, created.ChangedAt), ct)
                    .ConfigureAwait(false);
                break;
            case ProgramRevised revised:
                var current = await ProgramDirectorySchema.Directory.GetAsync(Transaction,
                    revised.ProgramId, ct).ConfigureAwait(false);
                if (current is not null)
                    await ProgramDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                        current with
                        {
                            Name = revised.Name,
                            Revision = revised.Revision,
                            Plan = revised.Plan,
                            LastChangedByMemberId = revised.ActorMemberId,
                            LastChangedByDisplay = revised.ActorDisplay,
                            LastChangedAt = revised.ChangedAt,
                        }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Directory.GetAsync(tx, programId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Directory.QueryAsync(tx,
            ProgramDirectorySchema.ByName.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }
}
