using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed partial class RiskDraftDirectoryProjector(IRiskDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risks"),
            "RiskDraftDirectory"),
      IProjectorHandler<RiskDraftCreated>, IProjectorHandler<RiskDraftRevised>
{
    public ValueTask HandleAsync(RiskDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
