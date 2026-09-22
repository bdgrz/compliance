using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed partial class CommitmentDraftHistoryDirectoryProjectorV1(
    ICommitmentDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
            "CommitmentDraftHistoryDirectoryV1"),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>
{
    public ValueTask HandleAsync(CommitmentDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
