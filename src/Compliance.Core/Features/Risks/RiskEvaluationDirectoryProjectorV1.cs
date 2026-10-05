using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed partial class RiskEvaluationDirectoryProjectorV1(
    IRiskEvaluationDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risk-evaluations"),
            FitzRiskEvaluationDirectory.ProjectorName),
      IProjectorHandler<RiskAssessmentRecorded>, IProjectorHandler<RiskTreatmentChosen>,
      IProjectorHandler<RiskAccepted>
{
    public ValueTask HandleAsync(RiskAssessmentRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskTreatmentChosen ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskAccepted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
