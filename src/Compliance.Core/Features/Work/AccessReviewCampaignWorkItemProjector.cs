using Bdgrz.Compliance.Features.AccessReviews;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class AccessReviewCampaignWorkItemProjector(
    IAccessReviewCampaignWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AccessReviewCampaign.Area),
        FitzAccessReviewCampaignWorkItemDirectory.ProjectorName),
      IProjectorHandler<AccessReviewCampaignLaunched>,
      IProjectorHandler<AccessDecisionsRecorded>,
      IProjectorHandler<AccessRemediationChangeRecorded>,
      IProjectorHandler<AccessRemediationVerified>,
      IProjectorHandler<AccessRemediationExceptionRecorded>,
      IProjectorHandler<AccessReviewResponsibilityReassigned>,
      IProjectorHandler<AccessReviewCampaignCompleted>
{
    public ValueTask HandleAsync(AccessReviewCampaignLaunched ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessDecisionsRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationChangeRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationVerified ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessRemediationExceptionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewResponsibilityReassigned ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AccessReviewCampaignCompleted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
