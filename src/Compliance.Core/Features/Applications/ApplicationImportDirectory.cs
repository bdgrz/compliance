using System.Globalization;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationImportDirectoryReader
{
    ValueTask<ApplicationImportView?> GetAsync(Uuid tenantId, Uuid batchId,
        CancellationToken ct = default);
    ValueTask<Page<ApplicationImportRowView>> ListRowsAsync(Uuid tenantId, Uuid batchId,
        int limit, string? cursor, CancellationToken ct = default);
}

public interface IApplicationImportDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default);
}

static class ApplicationImportDirectorySchema
{
    public static readonly KvDirectory<ApplicationImportView, Uuid> Batches = new(
        "application_import_batches", ComplianceCoreJsonContext.Default.ApplicationImportView,
        static batch => batch.BatchId, static id => [id.ToString()], []);

    public static readonly KvDirectoryIndex<ApplicationImportRowView> ByBatchOrdinal = new(
        "by_batch_ordinal", 1, static row =>
            [row.BatchId.ToString(), row.RowNumber.ToString("D4", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ApplicationImportRowView, Uuid> Rows = new(
        "application_import_rows", ComplianceCoreJsonContext.Default.ApplicationImportRowView,
        static row => row.RowId, static id => [id.ToString()], [ByBatchOrdinal]);
}

sealed class FitzApplicationImportDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/application-import-directory/projection",
        "ApplicationImportDirectoryV1"), IApplicationImportDirectoryReader,
        IApplicationImportDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default)
    {
        switch (ev)
        {
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
        }
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
