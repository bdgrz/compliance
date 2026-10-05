using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class CriterionApplicabilityWorkItemProjector(
    ICriterionApplicabilityWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("criterion-applicability"),
        FitzCriterionApplicabilityWorkItemDirectory.ProjectorName),
      IProjectorHandler<CriterionNotApplicableProposed>,
      IProjectorHandler<CriterionApplicabilityReviewed>,
      IProjectorHandler<CriterionNotApplicableWithdrawn>
{
    public ValueTask HandleAsync(CriterionNotApplicableProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CriterionApplicabilityReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CriterionNotApplicableWithdrawn ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
