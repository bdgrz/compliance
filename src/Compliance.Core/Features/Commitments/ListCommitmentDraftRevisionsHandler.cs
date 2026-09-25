using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ListCommitmentDraftRevisionsHandler(
    ICommitmentDraftHistoryDirectoryReader directory,
    CommitmentDraftHistoryReadConsistency consistency)
    : IRequestHandler<ListCommitmentDraftRevisions, Page<CommitmentDraftRevisionView>>
{
    public async ValueTask<Result<Page<CommitmentDraftRevisionView>>> HandleAsync(
        IRequestContext<ListCommitmentDraftRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The draft revision list limit must be between 1 and 200."));
        var fresh = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.DraftId, request.MinimumDraftRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<CommitmentDraftRevisionView>>.Failure(fresh.Error);
        Page<CommitmentDraftRevisionView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.DraftId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.DraftId != request.DraftId)
            ? Result<Page<CommitmentDraftRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The draft history projection has an invalid scope."))
            : Result<Page<CommitmentDraftRevisionView>>.Success(page);
    }
}
