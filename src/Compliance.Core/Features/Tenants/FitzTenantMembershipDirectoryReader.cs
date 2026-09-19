using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Serves the "TenantMembership" projector: writes join its batch transaction, and query-side
///     reads open their own read-only transaction on the resource it writes for the given tenant.
/// </summary>
sealed class FitzTenantMembershipDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TenantMembershipDirectoryKeys.Route, "TenantMembership"),
      ITenantMembershipDirectoryProjection,
      ITenantMembershipDirectoryReader
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

    public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId, ct).ConfigureAwait(false);
        return await TenantMembershipDirectorySchema.Directory.GetAsync(tx, userId, ct).ConfigureAwait(false) is not null;
    }
}
