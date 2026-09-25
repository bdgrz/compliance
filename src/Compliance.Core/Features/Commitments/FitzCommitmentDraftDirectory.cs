using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

sealed class FitzCommitmentDraftDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/commitment-draft-directory/projection",
            "CommitmentDraftDirectory"),
        ICommitmentDraftDirectoryReader, ICommitmentDraftDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("CommitmentDraftDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case CommitmentDraftCreated created:
                await CommitmentDraftDirectorySchema.Drafts.InsertAsync(Transaction,
                        new CommitmentDraftView(created.TenantId, created.ProgramId,
                            created.DraftId, created.ServiceId, created.Kind, created.Identifier,
                            1, "draft", "unverified", "unresolved", "unresolved",
                            created.Statement, created.Context, created.SourceReference,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                        {
                            LastChangedBy = created.Actor,
                        }, ct)
                    .ConfigureAwait(false);
                await CommitmentDraftDirectorySchema.Revisions.InsertAsync(Transaction,
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
                var current = await CommitmentDraftDirectorySchema.Drafts.GetAsync(Transaction,
                    revised.DraftId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.ProgramId != revised.ProgramId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A commitment draft revision cannot project before its predecessor.");
                await CommitmentDraftDirectorySchema.Drafts.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        Statement = revised.Statement,
                        Context = revised.Context,
                        SourceReference = revised.SourceReference,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await CommitmentDraftDirectorySchema.Revisions.InsertAsync(Transaction,
                    new CommitmentDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.DraftId, current.ServiceId, current.Kind, current.Identifier,
                        revised.Revision, revised.Statement, revised.Context,
                        revised.SourceReference, revised.ActorMemberId,
                        revised.ActorDisplay, revised.ChangedAt)
                    {
                        Actor = revised.Actor,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<CommitmentDraftView?> GetAsync(Uuid tenantId, Uuid draftId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CommitmentDraftDirectorySchema.Drafts.GetAsync(tx, draftId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<CommitmentDraftView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CommitmentDraftDirectorySchema.Drafts.QueryAsync(tx,
            CommitmentDraftDirectorySchema.ByProgramKindIdentifier.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid draftId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CommitmentDraftDirectorySchema.Revisions.GetAsync(tx,
                CommitmentDraftDirectorySchema.RevisionKey(draftId, revision), ct)
            .ConfigureAwait(false);
    }
}
