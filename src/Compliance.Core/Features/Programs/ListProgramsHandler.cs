using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListProgramsHandler(IProgramDirectoryReader directory)
    : IRequestHandler<ListPrograms, Page<ProgramView>>
{
    public async ValueTask<Result<Page<ProgramView>>> HandleAsync(
        IRequestContext<ListPrograms> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program list limit must be between 1 and 200."));
        Page<ProgramView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The programs were not found."))
            : Result<Page<ProgramView>>.Success(page);
    }
}
