using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class AccessReviewScopeDirectoryProjector(
    IAccessReviewScopeDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewScopeStreams.Area),
            AccessReviewScopeStreams.ProjectorName),
        IProjectorHandler<AccessReviewScopeDecided>
{
    public ValueTask HandleAsync(AccessReviewScopeDecided ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
