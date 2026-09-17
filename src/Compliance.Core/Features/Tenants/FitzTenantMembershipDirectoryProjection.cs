using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Materializes <see cref="MemberRegistered" /> into a global, cross-tenant membership index.
///     Unlike every other per-tenant directory projection in this codebase, this one writes a fixed
///     global KV route regardless of <see cref="WorkloadContext.Identity" />'s tenant — the whole
///     point is one index a user's memberships across every tenant land in, since "which tenants do
///     I belong to" is not itself a per-tenant question.
/// </summary>
sealed class FitzTenantMembershipDirectoryProjection(IKvClient client)
    : FitzKvProjectionStore(client, TenantMembershipDirectoryKeys.Route()), ITenantMembershipDirectoryProjection
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
}
