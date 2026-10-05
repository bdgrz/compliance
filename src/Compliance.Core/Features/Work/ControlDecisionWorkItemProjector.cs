using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class ControlDecisionWorkItemProjector(
    IControlDecisionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("controls"),
        FitzControlDecisionWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>,
      IProjectorHandler<ControlDraftDiscarded>, IProjectorHandler<ControlSuccessorProposed>,
      IProjectorHandler<ControlProposalWithdrawn>, IProjectorHandler<ControlReviewed>,
      IProjectorHandler<ControlApproved>, IProjectorHandler<ControlRetirementProposed>,
      IProjectorHandler<ControlRetired>, IProjectorHandler<ResponsibilityAssigned>,
      IProjectorHandler<ResponsibilityRevoked>
{
    public ValueTask HandleAsync(ControlDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlSuccessorProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlProposalWithdrawn ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlRetirementProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityAssigned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ResponsibilityRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
