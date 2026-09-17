using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzTeamMemberDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), ITeamMemberDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamMemberAssigned assigned:
                await TeamMemberDirectorySchema.Directory.InsertAsync(
                    Transaction,
                    new TeamMemberView(assigned.TeamId, assigned.MemberId),
                    ct).ConfigureAwait(false);
                break;
            case TeamMemberRemoved removed:
                await TeamMemberDirectorySchema.Directory.DeleteAsync(
                    Transaction,
                    (removed.TeamId, removed.MemberId),
                    ct).ConfigureAwait(false);
                break;
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The team-member directory projection requires a tenant workload.");
        return TeamMemberDirectoryKeys.Route(tenant.Value);
    }
}
