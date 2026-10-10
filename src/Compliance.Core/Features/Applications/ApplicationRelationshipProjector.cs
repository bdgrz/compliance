using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class ApplicationRelationshipProjector(
    IApplicationRelationshipProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("applications"),
        "ApplicationRelationshipsV1"),
      IProjectorHandler<ApplicationRelationshipRecorded>,
      IProjectorHandler<ApplicationRelationshipRemoved>,
      IProjectorHandler<ApplicationRelationshipApproved>
{
    public ValueTask HandleAsync(ApplicationRelationshipRecorded ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipRemoved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ApplicationRelationshipApproved ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
