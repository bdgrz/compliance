using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed partial class AcceptedStaffDirectoryReconciliationReactor(IProjectionCheckpointStore checkpoints,
    DirectoryReevaluationDiscovery discovery)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("client-independence"), WorkloadName),
        IReactorHandler<ServiceEngagementAcceptanceRecorded>
{
    public const string WorkloadName = "AcceptedStaffDirectoryReconciliationV1";

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementAcceptanceRecorded> context, CancellationToken ct)
    {
        if (Pattern.IsTenantTemplate || Pattern.Realm != context.Trigger.TenantId.ToString() ||
            context.Source.Stream != new IndependenceLedger(context.Trigger.TenantId).Stream)
            throw new InvalidOperationException("Accepted staff reconciliation requires its bound authoritative client ledger.");
        return discovery.ReconcileAcceptanceAsync(context, ct);
    }
}
