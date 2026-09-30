using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class PreviewCommitmentImpactHandler(CommitmentImpactService service)
    : IRequestHandler<PreviewCommitmentImpact, CommitmentImpactPreview>
{
    public ValueTask<Result<CommitmentImpactPreview>> HandleAsync(
        IRequestContext<PreviewCommitmentImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
