using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class BoundaryDecisionWorkItemProjector(
    IBoundaryDecisionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("boundaries"),
        FitzBoundaryDecisionWorkItemDirectory.ProjectorName),
      IProjectorHandler<BoundaryDraftCreated>, IProjectorHandler<BoundaryDraftRevised>,
      IProjectorHandler<BoundaryDraftDiscarded>, IProjectorHandler<BoundaryReviewed>,
      IProjectorHandler<BoundaryApproved>, IProjectorHandler<BoundarySuccessorProposed>,
      IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public ValueTask HandleAsync(BoundaryDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundaryApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(BoundarySuccessorProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityAssigned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
