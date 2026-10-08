using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed partial class FirmStaffStatusReevaluationReactor(IProjectionCheckpointStore checkpoints,
    ITenantDirectory tenants, IAggregateReader reader, DirectoryReevaluationDiscovery discovery)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "firm-staff-directory", new FirmStaffDirectory().Stream.Resource), WorkloadName),
        IReactorHandler<FirmStaffChangeRecorded>
{
    public const string WorkloadName = "FirmStaffStatusReevaluationV1";

    public async ValueTask HandleAsync(IReactorContext<FirmStaffChangeRecorded> context, CancellationToken ct)
    {
        if (context.Source.Stream != new FirmStaffDirectory().Stream)
            throw new InvalidOperationException("Staff status reconciliation requires its authoritative directory stream.");
        if (context.Trigger.Operation != "status")
            return;
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        if (!directory.MatchesRetainedStatus(context.Trigger))
            throw new InvalidOperationException("Staff status reconciliation requires its retained original directory decision.");
        var seen = new HashSet<Uuid>();
        await foreach (var tenant in tenants.GetActiveTenantsAsync(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (!Uuid.TryParse(tenant.Value, null, out var tenantId) || tenantId == Uuid.Empty)
                throw new InvalidOperationException("Registered assignment discovery requires canonical tenant identities.");
            if (seen.Add(tenantId))
                await discovery.ReconcileStatusAsync(tenantId, context, ct).ConfigureAwait(false);
        }
    }
}
