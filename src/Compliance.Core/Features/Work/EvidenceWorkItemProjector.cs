using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed partial class EvidenceWorkItemProjector(IEvidenceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-requests"),
        FitzEvidenceWorkItemDirectory.ProjectorName),
      IProjectorHandler<EvidenceRequestOpened>, IProjectorHandler<EvidenceRequestFulfilled>,
      IProjectorHandler<EvidenceRequestCancelled>
{
    public ValueTask HandleAsync(EvidenceRequestOpened ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(EvidenceRequestFulfilled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(EvidenceRequestCancelled ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
