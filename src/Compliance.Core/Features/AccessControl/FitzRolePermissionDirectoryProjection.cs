using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzRolePermissionDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), IRolePermissionDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RolePermissionAssigned assigned:
                await RolePermissionDirectorySchema.Directory.InsertAsync(
                    Transaction,
                    new RolePermissionView(assigned.RoleId, assigned.Permission),
                    ct).ConfigureAwait(false);
                break;
            case RolePermissionRemoved removed:
                await RolePermissionDirectorySchema.Directory.DeleteAsync(
                    Transaction,
                    (removed.RoleId, removed.Permission),
                    ct).ConfigureAwait(false);
                break;
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The role-permission directory projection requires a tenant workload.");
        return RolePermissionDirectoryKeys.Route(tenant.Value);
    }
}
