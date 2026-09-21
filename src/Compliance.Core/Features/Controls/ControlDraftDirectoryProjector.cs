using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed partial class ControlDraftDirectoryProjector(IControlDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("controls"),
            "ControlDraftDirectory"),
      IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>
{
    public ValueTask HandleAsync(ControlDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
