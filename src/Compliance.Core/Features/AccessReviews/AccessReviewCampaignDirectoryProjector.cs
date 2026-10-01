using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

public sealed partial class AccessReviewCampaignDirectoryProjector(
    IAccessReviewCampaignDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewCampaign.Area),
            AccessReviewDirectorySchema.CampaignProjector),
        IProjectorHandler<AccessReviewCampaignLaunched>,
        IProjectorHandler<AccessReviewCampaignCompleted>
{
    public ValueTask HandleAsync(AccessReviewCampaignLaunched ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewCampaignCompleted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
