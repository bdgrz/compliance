using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ListControlDraftsHandler(IControlDraftDirectoryReader directory,
    IAggregateReader reader, ControlDraftListReadConsistency consistency)
    : IRequestHandler<ListControlDrafts, Page<ControlDraftView>>
{
    public async ValueTask<Result<Page<ControlDraftView>>> HandleAsync(
        IRequestContext<ListControlDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ControlDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<ControlDraftView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ControlDraftView>>.Failure(ready.Error);
        Page<ControlDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ControlDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The control draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<ControlDraftView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The control draft projection has an invalid program scope."))
            : Result<Page<ControlDraftView>>.Success(page);
    }
}
