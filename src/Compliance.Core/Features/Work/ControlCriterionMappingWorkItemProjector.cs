using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class ControlCriterionMappingWorkItemProjector(
    IControlCriterionMappingWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-criterion-mappings"),
        FitzControlMappingWorkItemDirectory.ProjectorName),
      IProjectorHandler<ControlCriterionMappingProposed>,
      IProjectorHandler<ControlCriterionMappingReviewed>,
      IProjectorHandler<ControlCriterionMappingRetired>
{
    public ValueTask HandleAsync(ControlCriterionMappingProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlCriterionMappingReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlCriterionMappingRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
