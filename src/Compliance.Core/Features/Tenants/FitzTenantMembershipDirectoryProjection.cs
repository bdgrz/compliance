using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class FitzTenantMembershipDirectoryProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), ITenantMembershipDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is MemberRegistered registered)
        {
            await TenantMembershipDirectorySchema.Directory.InsertAsync(
                Transaction,
                new TenantMembershipView(registered.UserId, registered.TenantId),
                ct).ConfigureAwait(false);
        }
    }

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The tenant-membership projection requires a tenant workload.");
        return TenantMembershipDirectoryKeys.Route(tenant.Value);
    }
}
