using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class PolicyCampaignWorkItemProjector(
    IPolicyCampaignWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policy-distribution-campaigns"),
        FitzPolicyCampaignWorkItemDirectory.ProjectorName),
      IProjectorHandler<PolicyCampaignLaunched>, IProjectorHandler<PolicyCampaignAudienceFrozen>,
      IProjectorHandler<PolicyCampaignAudienceAmended>,
      IProjectorHandler<PolicyAcknowledgementRecorded>,
      IProjectorHandler<TrainingCompletionRecorded>, IProjectorHandler<CampaignWaiverApproved>,
      IProjectorHandler<PolicyCampaignClosed>
{
    public ValueTask HandleAsync(PolicyCampaignLaunched ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyCampaignAudienceFrozen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyCampaignAudienceAmended ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyAcknowledgementRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TrainingCompletionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CampaignWaiverApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyCampaignClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
