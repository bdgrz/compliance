using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class GetEffectiveCommitmentVersionHandler(
    CommitmentVersionReadConsistency consistency)
    : IRequestHandler<GetEffectiveCommitmentVersion, CommitmentVersionView>
{
    public async ValueTask<Result<CommitmentVersionView>> HandleAsync(
        IRequestContext<GetEffectiveCommitmentVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await consistency.SourceAsync(request.TenantId, request.ProgramId,
            request.DraftId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CommitmentVersionView>.Failure(source.Error);
        return source.Value.EffectiveVersionOn(request.EffectiveOn) is { } version
            ? await consistency.GetAsync(request.TenantId, request.ProgramId, request.DraftId,
                version, ct).ConfigureAwait(false)
            : Result<CommitmentVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No commitment version was effective on that date."));
    }
}
