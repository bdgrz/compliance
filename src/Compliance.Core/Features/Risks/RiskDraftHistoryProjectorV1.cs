using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed partial class RiskDraftHistoryProjectorV1(
    IRiskDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risks"),
            "RiskDraftHistoryDirectoryV1"),
      IProjectorHandler<RiskDraftCreated>, IProjectorHandler<RiskDraftRevised>
{
    public ValueTask HandleAsync(RiskDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
