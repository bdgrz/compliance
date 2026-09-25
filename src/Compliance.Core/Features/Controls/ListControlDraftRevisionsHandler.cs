using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ListControlDraftRevisionsHandler(IControlDraftHistoryDirectoryReader directory,
    ControlDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListControlDraftRevisions, Page<ControlDraftRevisionView>>
{
    public async ValueTask<Result<Page<ControlDraftRevisionView>>> HandleAsync(
        IRequestContext<ListControlDraftRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The control draft revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.MinimumControlDraftRevision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ControlDraftRevisionView>>.Failure(freshness.Error);
        Page<ControlDraftRevisionView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ControlId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.ControlId != request.ControlId)
            ? Result<Page<ControlDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The control draft history projection is incomplete.", isTransient: true))
            : Result<Page<ControlDraftRevisionView>>.Success(page);
    }
}
