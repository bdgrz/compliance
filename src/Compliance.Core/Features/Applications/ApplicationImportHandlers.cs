using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class StageApplicationImportHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<StageApplicationImport, ApplicationImportRegistration>
{
    public ValueTask<Result<ApplicationImportRegistration>> HandleAsync(
        IRequestContext<StageApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        return executor.ExecuteAsync(new ImportBatch(request.TenantId, ImportBatch.BatchIdFor(request)),
            batch => AggregateOutcome.CommitOnSuccess(batch.Stage(request, memberId,
                display, clock.GetUtcNow())), context, ct);
    }
}

public sealed class CancelApplicationImportHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CancelApplicationImport>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CancelApplicationImport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        return executor.ExecuteAsync(new ImportBatch(request.TenantId, request.BatchId),
            batch => AggregateOutcome.CommitOnSuccess(batch.Cancel(request.ExpectedBatchRevision,
                request.Reason, memberId, display, clock.GetUtcNow())), context, ct);
    }
}

public sealed class ApplicationImportReadConsistency(IApplicationImportDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<ApplicationImportView>> GetFreshAsync(Uuid tenantId,
        Uuid batchId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum import revision must be positive."));
        var source = await reader.HydrateAsync(new ImportBatch(tenantId, batchId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found."));
        if (minimumRevision is { } minimum && source.Revision < minimum)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, $"The import source has not reached revision {minimum}.",
                isTransient: true));
        var view = await directory.GetAsync(tenantId, batchId, ct).ConfigureAwait(false);
        return view is null || view.TenantId != tenantId || view.BatchId != batchId ||
               view.Revision < source.Revision ||
               (minimumRevision is { } wanted && view.Revision < wanted)
            ? Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import projection has not reached the source revision.",
                isTransient: true))
            : Result<ApplicationImportView>.Success(view);
    }
}

public sealed class GetApplicationImportHandler(ApplicationImportReadConsistency consistency)
    : IRequestHandler<GetApplicationImport, ApplicationImportView>
{
    public ValueTask<Result<ApplicationImportView>> HandleAsync(
        IRequestContext<GetApplicationImport> context, CancellationToken ct) =>
        consistency.GetFreshAsync(context.Request.TenantId, context.Request.BatchId,
            context.Request.MinimumRevision, ct);
}

public sealed class ListApplicationImportRowsHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency)
    : IRequestHandler<ListApplicationImportRows, Page<ApplicationImportRowView>>
{
    public async ValueTask<Result<Page<ApplicationImportRowView>>> HandleAsync(
        IRequestContext<ListApplicationImportRows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import row limit must be between 1 and 200."));
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportRowView>>.Failure(fresh.Error);
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import row cursor is invalid."));
        }
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (request.Cursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportRowView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        return Result<Page<ApplicationImportRowView>>.Success(page);
    }
}

public sealed class PreviewApplicationImportHandler(
    IApplicationImportDirectoryReader directory, ApplicationImportReadConsistency consistency)
    : IRequestHandler<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>
{
    public async ValueTask<Result<Page<ApplicationImportPreviewRow>>> HandleAsync(
        IRequestContext<PreviewApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview limit must be between 1 and 200."));
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportPreviewRow>>.Failure(fresh.Error);
        Page<ApplicationImportRowView> page;
        try
        {
            page = await directory.ListRowsAsync(request.TenantId, request.BatchId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The import preview cursor is invalid."));
        }
        if (page.Items.Any(row => row.TenantId != request.TenantId ||
                                  row.BatchId != request.BatchId) ||
            (request.Cursor is null && page.Items.Count == 0 && fresh.Value.RowCount > 0))
            return Result<Page<ApplicationImportPreviewRow>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import row projection is incomplete.",
                isTransient: true));
        return Result<Page<ApplicationImportPreviewRow>>.Success(new Page<ApplicationImportPreviewRow>(
            page.Items.Select(row => new ApplicationImportPreviewRow(row.TenantId, row.BatchId,
                row.RowId, row.RowNumber, row.SourceRecordId, row.Name, row.Purpose,
                row.OwnerReference, row.ValidationFindings,
                row.ValidationFindings.Contains("duplicate_source_record_id")
                    ? "duplicate" : row.ValidationFindings.Count > 0 ? "invalid" : "unmatched",
                [], [], [.. row.ValidationFindings, "source_claims_unavailable"]))
                .ToArray(), page.NextCursor));
    }
}
