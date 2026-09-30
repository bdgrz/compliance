using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ListCommitmentDecisionsHandler(ICommitmentDraftDirectoryReader directory,
    CommitmentVersionReadConsistency consistency)
    : IRequestHandler<ListCommitmentDecisions, Page<CommitmentDecisionView>>
{
    public async ValueTask<Result<Page<CommitmentDecisionView>>> HandleAsync(
        IRequestContext<ListCommitmentDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CommitmentDecisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The decision list limit must be between 1 and 200."));
        var fresh = await consistency.EnsureVersionsAsync(request.TenantId, request.ProgramId,
            request.DraftId, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<CommitmentDecisionView>>.Failure(fresh.Error);
        Page<CommitmentDecisionView> page;
        try
        {
            page = await directory.ListDecisionsAsync(request.TenantId, request.DraftId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CommitmentDecisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The decision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.DraftId != request.DraftId)
            ? Result<Page<CommitmentDecisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The decision projection has an invalid scope."))
            : Result<Page<CommitmentDecisionView>>.Success(page);
    }
}
