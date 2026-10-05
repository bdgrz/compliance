using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class PolicyDecisionWorkItemProjector(
    IPolicyDecisionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("policies"),
        FitzPolicyDecisionWorkItemDirectory.ProjectorName),
      IProjectorHandler<PolicyDraftCreated>, IProjectorHandler<PolicyDraftRevised>,
      IProjectorHandler<PolicyReviewed>, IProjectorHandler<PolicyApproved>,
      IProjectorHandler<PolicyPeriodicReviewConfirmed>,
      IProjectorHandler<PolicyRetirementProposed>, IProjectorHandler<PolicyRetired>,
      IProjectorHandler<PolicyDraftDiscarded>
{
    public ValueTask HandleAsync(PolicyDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyPeriodicReviewConfirmed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyRetirementProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PolicyDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
