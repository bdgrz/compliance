using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ListProgramClientServicesHandler(IClientServiceDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<ListProgramClientServices, Page<ClientServiceView>>
{
    public async ValueTask<Result<Page<ClientServiceView>>> HandleAsync(
        IRequestContext<ListProgramClientServices> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The program client service list limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        Page<ClientServiceView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ClientServiceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The program client service cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<ClientServiceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program services were not found."))
            : Result<Page<ClientServiceView>>.Success(page);
    }
}
