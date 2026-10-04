using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public sealed class ListDataFlowRevisionsHandler(IAggregateReader aggregates,
    ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency, TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListDataFlowRevisions, Page<DataFlowView>>
{
    public async ValueTask<Result<Page<DataFlowView>>> HandleAsync(
        IRequestContext<ListDataFlowRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The data flow revision list limit must be between 1 and 200."));
        if (request.MinimumRevision is < 1)
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The minimum data flow revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var source = await aggregates.HydrateAsync(new DataFlow(request.TenantId,
            request.DataFlowId), ct).ConfigureAwait(false);
        if (!source.IsCreated || !await visibility.CanReadFlowAsync(request.TenantId, userId,
                source.Id, source.Content?.Classification, ct).ConfigureAwait(false))
            return Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        var fence = await consistency.CaptureAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<DataFlowView>>.Failure(fence.Error);
        var current = await consistency.GetFlowAsync(request.TenantId, request.DataFlowId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<DataFlowView>>.Failure(current.Error);
        if (!await visibility.CanReadFlowAsync(request.TenantId, userId, current.Value, ct)
                .ConfigureAwait(false))
            return Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The data flow was not found."));
        Page<DataFlowView> page;
        try
        {
            page = await VisibleTechnologyInventoryPage.ReadAsync(request.Limit ?? 50,
                request.Cursor,
                (limit, cursor) => directory.ListFlowRevisionsAsync(request.TenantId,
                    request.DataFlowId, limit, cursor, ct),
                item => visibility.CanReadFlowAsync(request.TenantId, userId, item, ct),
                item => item.TenantId == request.TenantId && item.DataFlowId == request.DataFlowId)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The data flow revision cursor is invalid."));
        }
        catch (VisibleTechnologyInventoryPage.ForeignDirectoryItemException)
        {
            return Result<Page<DataFlowView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The data flow revision projection is incomplete."));
        }
        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(request.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        return unchanged.IsSuccess
            ? Result<Page<DataFlowView>>.Success(page)
            : Result<Page<DataFlowView>>.Failure(unchanged.Error);
    }
}
