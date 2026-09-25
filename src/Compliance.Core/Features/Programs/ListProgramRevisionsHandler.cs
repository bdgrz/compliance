using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListProgramRevisionsHandler(IProgramDirectoryReader directory,
    ProgramHistoryReadConsistency consistency)
    : IRequestHandler<ListProgramRevisions, Page<ProgramRevisionView>>
{
    public async ValueTask<Result<Page<ProgramRevisionView>>> HandleAsync(
        IRequestContext<ListProgramRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The program revision list limit must be between 1 and 200."));
        if (request.MinimumProgramRevision is { } minimum)
        {
            var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
                minimum, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<Page<ProgramRevisionView>>.Failure(freshness.Error);
        }
        Page<ProgramRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The program revision cursor is invalid."));
        }
        return page is null
            ? Result<Page<ProgramRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."))
            : page.Items.Any(item => item.ProgramId != request.ProgramId)
                ? Result<Page<ProgramRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The program revision projection is incomplete."))
                : Result<Page<ProgramRevisionView>>.Success(page);
    }
}
