using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed partial class ControlMappingDirectoryV1Projector(
    IControlMappingDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("control-criterion-mappings"),
            "ControlMappingDirectoryV1"),
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
