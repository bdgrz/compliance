using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed partial class AssuranceProjector(IAssuranceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ProviderAssuranceRegister.Area), FitzAssuranceDirectory.ProjectorName),
      IProjectorHandler<AssuranceReportRecorded>, IProjectorHandler<AssuranceReportRevised>,
      IProjectorHandler<ProviderReviewRecorded>, IProjectorHandler<ProviderCoverageGapRecorded>,
      IProjectorHandler<ProviderCoverageGapClosed>,
      IProjectorHandler<ProviderCoverageGapRiskAcceptanceLinked>
{
    public ValueTask HandleAsync(AssuranceReportRecorded ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AssuranceReportRevised ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProviderReviewRecorded ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProviderCoverageGapRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProviderCoverageGapClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProviderCoverageGapRiskAcceptanceLinked ev,
        IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
