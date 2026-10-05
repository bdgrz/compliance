using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class CorrectiveActionWorkItemProjector(
    ICorrectiveActionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("remediation"),
        FitzCorrectiveActionWorkItemDirectory.ProjectorName),
      IProjectorHandler<FindingRaised>, IProjectorHandler<FindingRevised>,
      IProjectorHandler<CorrectiveActionAdded>, IProjectorHandler<CorrectiveActionCompleted>,
      IProjectorHandler<FindingAcceptanceLinked>, IProjectorHandler<FindingClosed>,
      IProjectorHandler<FindingReopened>
{
    public ValueTask HandleAsync(FindingRaised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(FindingRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CorrectiveActionAdded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CorrectiveActionCompleted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(FindingAcceptanceLinked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(FindingClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(FindingReopened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
