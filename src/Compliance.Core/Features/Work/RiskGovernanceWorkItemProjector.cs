using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class RiskGovernanceWorkItemProjector(
    IRiskGovernanceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risk-governance"),
        FitzRiskGovernanceWorkItemDirectory.ProjectorName),
      IProjectorHandler<RiskOwnerAssigned>, IProjectorHandler<RiskControlTreatmentProposed>,
      IProjectorHandler<RiskControlTreatmentReviewed>, IProjectorHandler<RiskControlTreatmentRetired>,
      IProjectorHandler<RiskReassessmentTriggered>, IProjectorHandler<RiskTreatmentActionAdded>,
      IProjectorHandler<RiskTreatmentActionRevised>, IProjectorHandler<RiskTreatmentActionCancelled>,
      IProjectorHandler<RiskTreatmentActionCompletionSubmitted>,
      IProjectorHandler<RiskTreatmentActionCompletionReviewed>
{
    public ValueTask HandleAsync(RiskOwnerAssigned ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskControlTreatmentProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskControlTreatmentReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskControlTreatmentRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskReassessmentTriggered ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentActionAdded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentActionRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentActionCancelled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentActionCompletionSubmitted ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentActionCompletionReviewed ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
