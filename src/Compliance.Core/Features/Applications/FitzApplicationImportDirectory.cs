using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationImportDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/application-import-directory/projection",
            "ApplicationImportDirectoryV1"), IApplicationImportDirectoryReader,
        IApplicationImportDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default)
    {
        switch (ev)
        {
            case ApplicationImportPlanStarted started:
                await ApplyPlanRevisionAsync(started.TenantId, started.BatchId, started.Revision, started.StartedAt, ct).ConfigureAwait(false);
                break;
            case ApplicationImportPlanRowFrozen frozen:
                await ApplyPlanRevisionAsync(frozen.TenantId, frozen.BatchId, frozen.Revision, null, ct).ConfigureAwait(false);
                break;
            case ApplicationImportPlanSealed sealedPlan:
                await ApplyPlanRevisionAsync(sealedPlan.TenantId, sealedPlan.BatchId, sealedPlan.Revision, null, ct).ConfigureAwait(false);
                break;
            case ApplicationImportStaged staged:
                var invalid = staged.Rows.Count(row => row.ValidationFindings.Count > 0);
                await ApplicationImportDirectorySchema.Batches.InsertAsync(Transaction,
                    new ApplicationImportView(staged.TenantId, staged.BatchId, staged.SubmissionId,
                        staged.SourceKey, staged.SourceNamespace, staged.Coverage,
                        staged.ContentSha256, 1, "preview_ready", staged.ActorMemberId,
                        staged.ActorDisplay, staged.SubmittedAt, staged.Rows.Count, invalid,
                        0, 0, 0, 0, staged.SubmittedAt), ct).ConfigureAwait(false);
                foreach (var row in staged.Rows)
                    await ApplicationImportDirectorySchema.Rows.InsertAsync(Transaction,
                            new ApplicationImportRowView(staged.TenantId, staged.BatchId, row.RowId,
                                row.RowNumber, row.SourceRecordId, row.Name, row.Purpose,
                                row.OwnerReference, row.ValidationFindings, "staged", null), ct)
                        .ConfigureAwait(false);
                break;
            case ApplicationImportCanceled canceled:
                var current = await ApplicationImportDirectorySchema.Batches.GetAsync(
                                  Transaction, canceled.BatchId, ct).ConfigureAwait(false) ??
                              throw new InvalidOperationException(
                                  "An import cancellation cannot project before its staging event.");
                await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = canceled.Revision,
                        State = "canceled",
                        LastProgressAt = canceled.CanceledAt,
                    }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRowCorrelated correlated:
                var batch = await ApplicationImportDirectorySchema.Batches.GetAsync(
                                Transaction, correlated.BatchId, ct).ConfigureAwait(false) ??
                            throw new InvalidOperationException("An import correlation cannot project before staging.");
                await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, batch,
                    batch with { Revision = correlated.Revision, LastProgressAt = correlated.RecordedAt }, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ApplyPlanRevisionAsync(Uuid tenantId, Uuid batchId, long revision,
        DateTimeOffset? startedAt, CancellationToken ct)
    {
        var current = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction, batchId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId)
            throw new InvalidOperationException("An import plan cannot project before its tenant's staging event.");
        await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, current,
            current with
            {
                Revision = revision,
                State = "accepting",
                LastProgressAt = startedAt ?? current.LastProgressAt,
            }, ct).ConfigureAwait(false);
    }

    public async ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationImportDirectorySchema.Batches.GetAsync(tx, batchId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId,
        Uuid batchId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ApplicationImportDirectorySchema.Rows.QueryAsync(tx,
                ApplicationImportDirectorySchema.ByBatchOrdinal.Query()
                    .WithPrefix(batchId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }
}
