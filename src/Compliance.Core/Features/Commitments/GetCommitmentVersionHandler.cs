using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class GetCommitmentVersionHandler(CommitmentVersionReadConsistency consistency)
    : IRequestHandler<GetCommitmentVersion, CommitmentVersionView>
{
    public ValueTask<Result<CommitmentVersionView>> HandleAsync(
        IRequestContext<GetCommitmentVersion> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.DraftId, context.Request.Version, ct);
}
