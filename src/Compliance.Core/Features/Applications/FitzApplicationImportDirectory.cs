using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationImportDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/application-import-directory/projection",
            "ApplicationImportDirectoryV1"), IApplicationImportDirectoryReader,
        IApplicationImportDirectoryProjection
{
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginImportBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginImportBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginImportBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An import projection batch requires its tenant realm.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default)
    {
        switch (ev)
        {
            case ApplicationImportFailed failed:
                var failedBatch = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction, failed.BatchId, ct).ConfigureAwait(false);
                if (failed.TenantId != _batchTenantId || failedBatch is null || failedBatch.TenantId != failed.TenantId ||
                    failedBatch.SourceKey != failed.SourceKey || failedBatch.SourceNamespace != failed.SourceNamespace ||
                    failedBatch.State != "accepting" || failedBatch.Revision + 1 != failed.Revision)
                    throw new InvalidOperationException("An import failure requires its exact accepting source batch.");
                await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, failedBatch, failedBatch with
                {
                    Revision = failed.Revision,
                    State = "failed",
                    PendingCount = 0,
                    AppliedCount = 0,
                    FailedCount = failedBatch.PendingCount,
                    LastProgressAt = failed.FailedAt,
                }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportCommitted committed:
                var committedBatch = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction,
                    committed.BatchId, ct).ConfigureAwait(false);
                if (committed.TenantId != _batchTenantId || committedBatch is null || committedBatch.TenantId != committed.TenantId ||
                    committedBatch.SourceKey != committed.SourceKey || committedBatch.SourceNamespace != committed.SourceNamespace ||
                    committedBatch.Revision + 1 != committed.Revision || committedBatch.State != "accepting" ||
                    committedBatch.PendingCount != committed.Effects.Count)
                    throw new InvalidOperationException("An import commit cannot project before its complete source plan.");
                await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, committedBatch,
                    committedBatch with
                    {
                        Revision = committed.Revision,
                        State = "committed",
                        AppliedCount = committed.Effects.Count,
                        PendingCount = 0,
                        LastProgressAt = committed.CommittedAt,
                    }, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRetirementProposalStarted retirementStart:
                await ApplyRetirementRevisionAsync(retirementStart.TenantId, retirementStart.BatchId,
                    retirementStart.Revision, retirementStart.PreparedAt, retirementStart, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRetirementRowFrozen retirementRow:
                await ApplyRetirementRevisionAsync(retirementRow.TenantId, retirementRow.BatchId,
                    retirementRow.Revision, null, null, ct).ConfigureAwait(false);
                break;
            case ApplicationImportRetirementProposalSealed retirementSeal:
                await ApplyRetirementRevisionAsync(retirementSeal.TenantId, retirementSeal.BatchId,
                    retirementSeal.Revision, null, null, ct).ConfigureAwait(false);
                break;
            case ApplicationImportPlanStarted started:
                await ApplyPlanRevisionAsync(started.TenantId, started.BatchId, started.Revision,
                    started.RowCount, started.StartedAt, ct).ConfigureAwait(false);
                break;
            case ApplicationImportPlanRowFrozen frozen:
                await ApplyPlanRevisionAsync(frozen.TenantId, frozen.BatchId, frozen.Revision, null, null, ct).ConfigureAwait(false);
                break;
            case ApplicationImportPlanSealed sealedPlan:
                await ApplyPlanRevisionAsync(sealedPlan.TenantId, sealedPlan.BatchId, sealedPlan.Revision, null, null, ct).ConfigureAwait(false);
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
                        PendingCount = 0,
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

    async ValueTask ApplyRetirementRevisionAsync(Uuid tenantId, Uuid batchId, long revision,
        DateTimeOffset? preparedAt, ApplicationImportRetirementProposalStarted? start, CancellationToken ct)
    {
        var current = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction, batchId, ct).ConfigureAwait(false);
        if (tenantId != _batchTenantId || current is null || current.TenantId != tenantId ||
            current.Coverage != "declared_complete" || current.State != "preview_ready" || current.Revision + 1 != revision ||
            (start is not null && (current.SourceKey != start.SourceKey || current.SourceNamespace != start.SourceNamespace ||
                current.ContentSha256 != start.ContentSha256)))
            throw new InvalidOperationException("A retirement proposal requires its exact preview-ready staged batch.");
        await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, current,
            current with { Revision = revision, LastProgressAt = preparedAt ?? current.LastProgressAt }, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyPlanRevisionAsync(Uuid tenantId, Uuid batchId, long revision,
        int? plannedRowCount, DateTimeOffset? startedAt, CancellationToken ct)
    {
        var current = await ApplicationImportDirectorySchema.Batches.GetAsync(Transaction, batchId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId)
            throw new InvalidOperationException("An import plan cannot project before its tenant's staging event.");
        await ApplicationImportDirectorySchema.Batches.ReplaceAsync(Transaction, current,
            current with
            {
                Revision = revision,
                State = "accepting",
                PendingCount = plannedRowCount ?? current.PendingCount,
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
