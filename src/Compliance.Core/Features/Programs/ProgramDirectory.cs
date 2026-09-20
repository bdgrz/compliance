using System.Globalization;
using Cntryl.Fitz;
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

    public static readonly KvDirectoryIndex<ProgramRevisionView> RevisionsByProgram = new(
        "by_program", 1,
        static revision => [revision.ProgramId.ToString(),
            revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ProgramRevisionView, string> Revisions = new(
        "program_revisions", ComplianceCoreJsonContext.Default.ProgramRevisionView,
        static revision => $"{revision.ProgramId}:{revision.Revision.ToString("D20", CultureInfo.InvariantCulture)}",
        static key => [key], [RevisionsByProgram]);
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
                        created.ActorMemberId, created.ActorDisplay, created.ChangedAt,
                        [
                            new ProgramStageView("readiness",
                                "Approve the scoped readiness baseline and own its remaining gaps."),
                            new ProgramStageView("type_i",
                                "Record the point-in-time examination outcome and Type II plan."),
                            new ProgramStageView("type_ii",
                                "Close the operating period and record the examination outcome."),
                        ]), ct)
                    .ConfigureAwait(false);
                await ProgramDirectorySchema.Revisions.InsertAsync(Transaction,
                    new ProgramRevisionView(created.ProgramId, 1, created.Name, created.Plan,
                        created.ActorMemberId, created.ActorDisplay, created.ChangedAt), ct)
                    .ConfigureAwait(false);
                break;
            case ProgramRevised revised:
                var current = await ProgramDirectorySchema.Directory.GetAsync(Transaction,
                    revised.ProgramId, ct).ConfigureAwait(false);
                if (current is null)
                    throw new InvalidOperationException(
                        "A program revision cannot project before its creation.");
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
                await ProgramDirectorySchema.Revisions.InsertAsync(Transaction,
                    new ProgramRevisionView(revised.ProgramId, revised.Revision,
                        revised.Name, revised.Plan, revised.ActorMemberId,
                        revised.ActorDisplay, revised.ChangedAt), ct).ConfigureAwait(false);
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

    public async ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        if (await ProgramDirectorySchema.Directory.GetAsync(tx, programId, ct).ConfigureAwait(false) is null)
            return null;
        return await ProgramDirectorySchema.Revisions.QueryAsync(tx,
            ProgramDirectorySchema.RevisionsByProgram.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid programId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ProgramDirectorySchema.Revisions.GetAsync(tx,
            $"{programId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}", ct)
            .ConfigureAwait(false);
    }
}
