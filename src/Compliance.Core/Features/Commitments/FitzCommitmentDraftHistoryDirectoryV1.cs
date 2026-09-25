using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class FitzCommitmentDraftHistoryDirectoryV1(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/commitment-draft-history-v1/projection",
            "CommitmentDraftHistoryDirectoryV1"),
        ICommitmentDraftHistoryDirectoryReader, ICommitmentDraftHistoryDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("CommitmentDraftHistoryDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case CommitmentDraftCreated created:
                await CommitmentDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction,
                        new CommitmentDraftRevisionView(created.TenantId, created.ProgramId,
                            created.DraftId, created.ServiceId, created.Kind, created.Identifier,
                            1, created.Statement, created.Context, created.SourceReference,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                        {
                            Actor = created.Actor,
                        }, ct)
                    .ConfigureAwait(false);
                break;
            case CommitmentDraftRevised revised:
                var predecessor = revised.Revision > 1
                    ? await CommitmentDraftHistoryDirectoryV1Schema.Revisions.GetAsync(
                        Transaction, CommitmentDraftHistoryDirectoryV1Schema.RevisionKey(
                            revised.DraftId, revised.Revision - 1), ct).ConfigureAwait(false)
                    : null;
                if (predecessor is null || predecessor.TenantId != revised.TenantId ||
                    predecessor.ProgramId != revised.ProgramId ||
                    predecessor.DraftId != revised.DraftId ||
                    predecessor.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A commitment draft history revision cannot project before its predecessor.");
                await CommitmentDraftHistoryDirectoryV1Schema.Revisions.InsertAsync(Transaction,
                    new CommitmentDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.DraftId, predecessor.ServiceId, predecessor.Kind,
                        predecessor.Identifier, revised.Revision, revised.Statement,
                        revised.Context, revised.SourceReference, revised.ActorMemberId,
                        revised.ActorDisplay, revised.ChangedAt)
                    {
                        Actor = revised.Actor,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid draftId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CommitmentDraftHistoryDirectoryV1Schema.Revisions.GetAsync(tx,
                CommitmentDraftHistoryDirectoryV1Schema.RevisionKey(draftId, revision), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<CommitmentDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
        Uuid draftId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CommitmentDraftHistoryDirectoryV1Schema.Revisions.QueryAsync(tx,
            CommitmentDraftHistoryDirectoryV1Schema.ByDraftRevision.Query()
                .WithPrefix(draftId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}
