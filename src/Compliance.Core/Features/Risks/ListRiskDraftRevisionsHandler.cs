using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ListRiskDraftRevisionsHandler(IRiskDraftHistoryDirectoryReader directory,
    RiskDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>
{
    public async ValueTask<Result<Page<RiskDraftRevisionView>>> HandleAsync(
        IRequestContext<ListRiskDraftRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The risk draft revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.MinimumRiskRevision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<RiskDraftRevisionView>>.Failure(freshness.Error);
        Page<RiskDraftRevisionView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.RiskId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk draft revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.RiskId != request.RiskId)
            ? Result<Page<RiskDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The risk draft history projection has an invalid scope."))
            : Result<Page<RiskDraftRevisionView>>.Success(page);
    }
}
