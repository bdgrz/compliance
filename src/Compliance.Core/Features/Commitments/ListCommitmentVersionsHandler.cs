using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ListCommitmentVersionsHandler(ICommitmentDraftDirectoryReader directory,
    CommitmentVersionReadConsistency consistency)
    : IRequestHandler<ListCommitmentVersions, Page<CommitmentVersionView>>
{
    public async ValueTask<Result<Page<CommitmentVersionView>>> HandleAsync(
        IRequestContext<ListCommitmentVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The version list limit must be between 1 and 200."));
        var fresh = await consistency.EnsureVersionsAsync(request.TenantId, request.ProgramId,
            request.DraftId, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<CommitmentVersionView>>.Failure(fresh.Error);
        Page<CommitmentVersionView> page;
        try
        {
            page = await directory.ListVersionsAsync(request.TenantId, request.DraftId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The version cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId ||
                                      item.DraftId != request.DraftId)
            ? Result<Page<CommitmentVersionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The version projection has an invalid scope."))
            : Result<Page<CommitmentVersionView>>.Success(page);
    }
}
