using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed partial class AccessPopulationDirectoryProjector(
    IAccessPopulationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessPopulation.Area),
            AccessReviewDirectorySchema.PopulationProjector),
        IProjectorHandler<AccessPopulationOpened>,
        IProjectorHandler<AccessPopulationFactsRecorded>,
        IProjectorHandler<AccessPopulationAccepted>
{
    public ValueTask HandleAsync(AccessPopulationOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessPopulationFactsRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessPopulationAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
