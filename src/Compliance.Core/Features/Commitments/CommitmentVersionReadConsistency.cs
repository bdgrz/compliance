using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Reads immutable effective versions after proving the projection reached the source.</summary>
public sealed class CommitmentVersionReadConsistency(ICommitmentDraftDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<CommitmentDraft>> SourceAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, CancellationToken ct)
    {
        var source = await reader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false);
        return !source.IsCreated || source.ProgramId != programId
            ? Result<CommitmentDraft>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The draft was not found."))
            : Result<CommitmentDraft>.Success(source);
    }

    public async ValueTask<Result<CommitmentVersionView>> GetAsync(Uuid tenantId,
        Uuid programId, Uuid draftId, long version, CancellationToken ct)
    {
        if (version < 1)
            return Result<CommitmentVersionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The version must be positive."));
        var source = await SourceAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CommitmentVersionView>.Failure(source.Error);
        if (version > source.Value.EffectiveVersionCount)
            return Result<CommitmentVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The commitment version was not found."));
        var view = await directory.GetVersionAsync(tenantId, draftId, version, ct)
            .ConfigureAwait(false);
        return view is not null && view.TenantId == tenantId && view.ProgramId == programId &&
               view.DraftId == draftId
            ? Result<CommitmentVersionView>.Success(view)
            : Result<CommitmentVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The commitment version projection has not reached the source.",
                isTransient: true));
    }

    public async ValueTask<Result> EnsureVersionsAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, CancellationToken ct)
    {
        var source = await SourceAsync(tenantId, programId, draftId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        if (source.Value.EffectiveVersionCount == 0)
            return Result.Success;
        var latest = await GetAsync(tenantId, programId, draftId,
            source.Value.EffectiveVersionCount, ct).ConfigureAwait(false);
        return latest.IsSuccess ? Result.Success : Result.Failure(latest.Error);
    }
}
