using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

sealed class FitzRiskDraftDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/risk-draft-directory/projection",
            "RiskDraftDirectory"),
        IRiskDraftDirectoryReader, IRiskDraftDirectoryProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("RiskDraftDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString(), "risks")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RiskDraftCreated created:
                await RiskDraftDirectorySchema.Risks.InsertAsync(Transaction,
                    new RiskDraftView(created.TenantId, created.ProgramId, created.RiskId,
                        created.Identifier, 1, "draft_unassessed", "unresolved",
                        created.Content, created.ActorMemberId, created.ActorDisplay,
                        created.ChangedAt)
                    {
                        LastChangedBy = created.Actor,
                    }, ct).ConfigureAwait(false);
                await RiskDraftDirectorySchema.Revisions.InsertAsync(Transaction,
                        new RiskDraftRevisionView(created.TenantId, created.ProgramId,
                            created.RiskId, created.Identifier, 1, created.Content,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                        {
                            Actor = created.Actor,
                        }, ct)
                    .ConfigureAwait(false);
                break;
            case RiskDraftRevised revised:
                var current = await RiskDraftDirectorySchema.Risks.GetAsync(Transaction,
                    revised.RiskId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.ProgramId != revised.ProgramId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A risk draft revision cannot project before its predecessor.");
                await RiskDraftDirectorySchema.Risks.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        Content = revised.Content,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await RiskDraftDirectorySchema.Revisions.InsertAsync(Transaction,
                    new RiskDraftRevisionView(revised.TenantId, revised.ProgramId,
                        revised.RiskId, current.Identifier, revised.Revision,
                        revised.Content, revised.ActorMemberId, revised.ActorDisplay,
                        revised.ChangedAt)
                    {
                        Actor = revised.Actor,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RiskDraftDirectorySchema.Risks.GetAsync(tx, riskId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RiskDraftDirectorySchema.Risks.QueryAsync(tx,
            RiskDraftDirectorySchema.ByProgramIdentifier.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
        Uuid riskId, long revision, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await RiskDraftDirectorySchema.Revisions.GetAsync(tx,
                RiskDraftDirectorySchema.RevisionKey(riskId, revision), ct)
            .ConfigureAwait(false);
    }
}
