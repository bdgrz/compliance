using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzRoleTeamDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), IRoleTeamDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamRoleAssigned assigned:
                await RoleTeamDirectorySchema.Directory.InsertAsync(
                    Transaction,
                    new RoleTeamView(assigned.RoleId, assigned.TeamId),
                    ct).ConfigureAwait(false);
                break;
            case TeamRoleRemoved removed:
                await RoleTeamDirectorySchema.Directory.DeleteAsync(
                    Transaction,
                    (removed.RoleId, removed.TeamId),
                    ct).ConfigureAwait(false);
                break;
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The role-team directory projection requires a tenant workload.");
        return RoleTeamDirectoryKeys.Route(tenant.Value);
    }
}
