using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ListCommitmentDraftsHandler(ICommitmentDraftDirectoryReader directory,
    IAggregateReader reader, CommitmentDraftListReadConsistency consistency)
    : IRequestHandler<ListCommitmentDrafts, Page<CommitmentDraftView>>
{
    public async ValueTask<Result<Page<CommitmentDraftView>>> HandleAsync(
        IRequestContext<ListCommitmentDrafts> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The program was not found."));
        var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<CommitmentDraftView>>.Failure(ready.Error);
        Page<CommitmentDraftView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ProgramId != request.ProgramId)
            ? Result<Page<CommitmentDraftView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The draft projection has an invalid program scope."))
            : Result<Page<CommitmentDraftView>>.Success(page);
    }
}
