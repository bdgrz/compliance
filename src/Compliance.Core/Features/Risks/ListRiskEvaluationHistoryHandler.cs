using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ListRiskEvaluationHistoryHandler(IRiskEvaluationDirectoryReader directory,
    RiskEvaluationReadConsistency consistency)
    : IRequestHandler<ListRiskEvaluationHistory, Page<RiskEvaluationHistoryEntryView>>
{
    public async ValueTask<Result<Page<RiskEvaluationHistoryEntryView>>> HandleAsync(
        IRequestContext<ListRiskEvaluationHistory> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The risk evaluation history limit must be between 1 and 200."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(freshness.Error);
        Page<RiskEvaluationHistoryEntryView> page;
        try
        {
            page = await directory.ListHistoryAsync(request.TenantId, request.RiskId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk evaluation history cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.RiskId != request.RiskId)
            ? Result<Page<RiskEvaluationHistoryEntryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The risk evaluation history projection has an invalid scope."))
            : Result<Page<RiskEvaluationHistoryEntryView>>.Success(page);
    }
}
