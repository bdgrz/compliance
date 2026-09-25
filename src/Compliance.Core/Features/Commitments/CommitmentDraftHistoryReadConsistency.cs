using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraftHistoryReadConsistency(
    ICommitmentDraftHistoryDirectoryReader directory, IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid programId, Uuid draftId,
        long? minimumDraftRevision, CancellationToken ct)
    {
        if (minimumDraftRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum draft revision must be positive."));
        var source = await reader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated || source.ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The draft was not found."));
        if (minimumDraftRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The draft source has not reached revision {minimum}.", isTransient: true));
        var latest = await directory.GetRevisionAsync(tenantId, draftId, source.Revision, ct)
            .ConfigureAwait(false);
        return latest is not null && latest.TenantId == tenantId &&
               latest.ProgramId == programId && latest.DraftId == draftId &&
               latest.Revision == source.Revision
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The draft history projection has not reached the source.", isTransient: true));
    }
}
