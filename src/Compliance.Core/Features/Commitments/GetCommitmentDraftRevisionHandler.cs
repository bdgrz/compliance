using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class GetCommitmentDraftRevisionHandler(
    ICommitmentDraftDirectoryReader directory, CommitmentDraftReadConsistency consistency)
    : IRequestHandler<GetCommitmentDraftRevision, CommitmentDraftRevisionView>
{
    public async ValueTask<Result<CommitmentDraftRevisionView>> HandleAsync(
        IRequestContext<GetCommitmentDraftRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Revision < 1)
            return Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft revision must be positive."));
        var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.DraftId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<CommitmentDraftRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.DraftId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ProgramId == request.ProgramId && revision.DraftId == request.DraftId
            ? Result<CommitmentDraftRevisionView>.Success(revision)
            : Result<CommitmentDraftRevisionView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The draft revision projection is incomplete.",
                isTransient: true));
    }
}
