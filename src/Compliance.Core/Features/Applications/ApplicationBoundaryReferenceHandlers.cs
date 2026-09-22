using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// A reverse lookup has no source-side record index. Check the projection's exact area cursor
/// before returning even an empty page, so initial backfill and worker lag are explicit.
/// </summary>
public sealed class ApplicationBoundaryReferenceReadConsistency(
    IApplicationBoundaryReferenceDirectory directory, IDomainEventReader events)
{
    public async ValueTask<Result> EnsureCaughtUpAsync(Uuid tenantId, CancellationToken ct)
    {
        var checkpoint = await directory.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        await using var pending = events.ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"),
                checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The boundary reference projection has not reached the source. Retry the query.",
                isTransient: true))
            : Result.Success;
    }
}

public sealed class ListApplicationBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
    : IRequestHandler<ListApplicationBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListApplicationBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application boundary reference list limit must be between 1 and 200."));
        var application = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (!application.IsCreated)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, "application",
                request.ApplicationId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.SubjectType != "application" ||
                                      item.GovernedRecordId != request.ApplicationId)
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."))
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }
}

public sealed class ListSystemInstanceBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency)
    : IRequestHandler<ListSystemInstanceBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListSystemInstanceBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance boundary reference list limit must be between 1 and 200."));
        var application = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (!application.HasInstance(request.SystemInstanceId))
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(ready.Error);
        Page<ApplicationBoundaryReferenceView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, "system_instance",
                request.SystemInstanceId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance boundary reference cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.SubjectType != "system_instance" ||
                                      item.GovernedRecordId != request.SystemInstanceId)
            ? Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."))
            : Result<Page<ApplicationBoundaryReferenceView>>.Success(page);
    }
}
