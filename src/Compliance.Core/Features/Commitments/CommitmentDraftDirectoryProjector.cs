using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed partial class CommitmentDraftDirectoryProjector(
    ICommitmentDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
            "CommitmentDraftDirectory"),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>
{
    public ValueTask HandleAsync(CommitmentDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
