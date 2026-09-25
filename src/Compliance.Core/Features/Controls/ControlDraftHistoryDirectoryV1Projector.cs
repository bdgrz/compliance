using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed partial class ControlDraftHistoryDirectoryV1Projector(
    IControlDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("controls"),
            "ControlDraftHistoryDirectoryV1"),
      IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>,
      IProjectorHandler<ControlDraftDiscarded>
{
    public ValueTask HandleAsync(ControlDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
