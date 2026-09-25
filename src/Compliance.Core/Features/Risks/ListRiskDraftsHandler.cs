using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ListRiskDraftsHandler(IRiskDraftDirectoryReader directory,
    IAggregateReader reader, RiskDraftListReadConsistency consistency)
    : IRequestHandler<ListRiskDrafts, Page<RiskDraftView>>
{
    public async ValueTask<Result<Page<RiskDraftView>>> HandleAsync(
        IRequestContext<ListRiskDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RiskDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<RiskDraftView>>.Failure(ready.Error);
        Page<RiskDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RiskDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<RiskDraftView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk draft projection has an invalid program scope."))
            : Result<Page<RiskDraftView>>.Success(page);
    }
}
