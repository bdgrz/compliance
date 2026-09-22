using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlDraftHistoryDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid controlId,
        long revision, CancellationToken ct = default);
    ValueTask<Page<ControlDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid controlId, int limit, string? cursor, CancellationToken ct = default);
}

public interface IControlDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

static class ControlDraftHistoryDirectoryV1Schema
{
    public static readonly KvDirectoryIndex<ControlDraftRevisionView> ByControlRevision = new(
        "by_control_revision", 1, static revision => [revision.ControlId.ToString(),
            revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ControlDraftRevisionView, string> Revisions = new(
        "control_draft_history_revisions_v1",
        ComplianceCoreJsonContext.Default.ControlDraftRevisionView,
        static revision => RevisionKey(revision.ControlId, revision.Revision),
        static key => [key], [ByControlRevision]);

    public static string RevisionKey(Uuid controlId, long revision) =>
        $"{controlId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}

sealed class FitzControlDraftHistoryDirectoryV1(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/control-draft-history-v1/projection",
            "ControlDraftHistoryDirectoryV1"),
      IControlDraftHistoryDirectoryReader, IControlDraftHistoryDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ControlDraftHistoryDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ControlDraftCreated created:
                await ControlDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction,
                    new ControlDraftRevisionView(created.TenantId, created.ProgramId,
                        created.ControlId, created.Identifier, 1, created.Content,
                        created.ActorMemberId, created.ActorDisplay, created.ChangedAt), ct)
                    .ConfigureAwait(false);
                break;
            case ControlDraftRevised revised:
                if (revised.Revision < 2)
                    throw new InvalidOperationException(
                        "A control draft history revision must follow its creation.");
                var predecessor = await ControlDraftHistoryDirectoryV1Schema.Revisions.GetAsync(
                    Transaction, ControlDraftHistoryDirectoryV1Schema.RevisionKey(revised.ControlId,
                        revised.Revision - 1), ct).ConfigureAwait(false);
                if (predecessor is null || predecessor.TenantId != revised.TenantId ||
                    predecessor.ProgramId != revised.ProgramId ||
                    predecessor.ControlId != revised.ControlId ||
                    predecessor.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A control draft history revision cannot project before its predecessor.");
                await ControlDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction,
                    new ControlDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.ControlId, predecessor.Identifier, revised.Revision,
                        revised.Content, revised.ActorMemberId, revised.ActorDisplay,
                        revised.ChangedAt), ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid controlId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlDraftHistoryDirectoryV1Schema.Revisions.GetAsync(tx,
            ControlDraftHistoryDirectoryV1Schema.RevisionKey(controlId, revision), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ControlDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid controlId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ControlDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(tx,
            ControlDraftHistoryDirectoryV1Schema.ByControlRevision.Query()
                .WithPrefix(controlId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
