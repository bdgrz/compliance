using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed partial class CriteriaTextOverlayDirectoryProjector(
    ICriteriaTextOverlayDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(CriteriaTextOverlayLedger.Area),
        FitzCriteriaTextOverlayDirectory.ProjectorName),
    IProjectorHandler<CriteriaTextOverlayEntryRevised>
{
    public ValueTask HandleAsync(CriteriaTextOverlayEntryRevised ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
