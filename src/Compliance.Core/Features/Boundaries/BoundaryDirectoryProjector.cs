using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed partial class BoundaryDirectoryProjector(IBoundaryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "BoundaryDirectory"),
      IProjectorHandler<BoundaryDraftCreated>, IProjectorHandler<BoundaryDraftRevised>,
      IProjectorHandler<BoundaryDraftDiscarded>,
      IProjectorHandler<BoundaryReviewed>, IProjectorHandler<BoundaryApproved>,
      IProjectorHandler<BoundarySuccessorProposed>
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
}
