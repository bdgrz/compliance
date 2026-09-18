using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzRoleDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), IRoleDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case RoleDefined defined:
                await RoleDirectorySchema.Directory.InsertAsync(
                    Transaction, new RoleView(defined.RoleId, defined.Name), ct).ConfigureAwait(false);
                break;
            case RoleDeleted deleted:
                await RoleDirectorySchema.Directory.DeleteAsync(Transaction, deleted.RoleId, ct).ConfigureAwait(false);
                break;
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The role directory projection requires a tenant workload.");
        return RoleDirectoryKeys.Route(tenant.Value);
    }
}
