using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class ActualStaffEngagementLocatorProjector(IActualStaffEngagementLocatorProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("client-independence"), WorkloadName)
{
    public const string WorkloadName = "ActualStaffEngagementLocatorV1";

    protected override ValueTask ProjectEventAsync(DomainEventRecord record, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(record, context, ct);
}
