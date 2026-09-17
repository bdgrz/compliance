using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzTeamDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), ITeamDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TeamDefined defined:
                await TeamDirectorySchema.Directory.InsertAsync(
                    Transaction, new TeamView(defined.TeamId, defined.Name), ct).ConfigureAwait(false);
                break;
            case TeamDeleted deleted:
                await TeamDirectorySchema.Directory.DeleteAsync(Transaction, deleted.TeamId, ct).ConfigureAwait(false);
                break;
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The team directory projection requires a tenant workload.");
        return TeamDirectoryKeys.Route(tenant.Value);
    }
}
