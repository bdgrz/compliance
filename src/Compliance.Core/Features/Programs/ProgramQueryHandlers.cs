using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetProgramHandler(IProgramDirectoryReader directory)
    : IRequestHandler<GetProgram, ProgramView>
{
    public async ValueTask<Result<ProgramView>> HandleAsync(IRequestContext<GetProgram> context,
        CancellationToken ct)
    {
        var program = await directory.GetAsync(context.Request.TenantId, context.Request.ProgramId, ct)
            .ConfigureAwait(false);
        return program is null
            ? Result<ProgramView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."))
            : Result<ProgramView>.Success(program);
    }
}

public sealed class ListProgramsHandler(IProgramDirectoryReader directory)
    : IRequestHandler<ListPrograms, Page<ProgramView>>
{
    public async ValueTask<Result<Page<ProgramView>>> HandleAsync(
        IRequestContext<ListPrograms> context, CancellationToken ct)
    {
        var page = await directory.ListAsync(context.Request.TenantId,
            context.Request.Limit ?? 50, context.Request.Cursor, ct).ConfigureAwait(false);
        return Result<Page<ProgramView>>.Success(page);
    }
}

public sealed class ListProgramRevisionsHandler(IProgramDirectoryReader directory)
    : IRequestHandler<ListProgramRevisions, Page<ProgramRevisionView>>
{
    public async ValueTask<Result<Page<ProgramRevisionView>>> HandleAsync(
        IRequestContext<ListProgramRevisions> context, CancellationToken ct)
    {
        var page = await directory.ListRevisionsAsync(context.Request.TenantId,
            context.Request.ProgramId, context.Request.Limit ?? 50,
            context.Request.Cursor, ct).ConfigureAwait(false);
        return page is null
            ? Result<Page<ProgramRevisionView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."))
            : Result<Page<ProgramRevisionView>>.Success(page);
    }
}
