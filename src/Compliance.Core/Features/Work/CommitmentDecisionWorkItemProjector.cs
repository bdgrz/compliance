using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class CommitmentDecisionWorkItemProjector(
    ICommitmentDecisionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
        FitzCommitmentDecisionWorkItemDirectory.ProjectorName),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>,
      IProjectorHandler<CommitmentReviewed>, IProjectorHandler<CommitmentApproved>,
      IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public ValueTask HandleAsync(CommitmentDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityAssigned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
