using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Serves the "TenantDirectory" projector, which is <see cref="WorkloadScope.Global" /> on
///     <see cref="EventStreamPattern.ForPattern(string, string)" />("bdgrz", "tenants"), so its
///     realm is the fixed "bdgrz" rather than a tenant ID. Writes join its batch transaction, and
///     query-side reads open their own read-only transaction on that same realm.
/// </summary>
sealed class FitzTenantDirectoryReader(IKvClient client)
    : FitzKvProjectionStore(client, TenantDirectoryKeys.Route, "TenantDirectory"),
      ITenantDirectoryProjection,
      ITenantDirectoryReader
{
    const string Realm = "bdgrz";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is TenantRegistered registered)
        {
            await TenantDirectorySchema.Directory.InsertAsync(
                Transaction,
                new TenantView(registered.TenantId, registered.Name, registered.Slug),
                ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await TenantDirectorySchema.Directory.GetAsync(tx, tenantId, ct).ConfigureAwait(false);
    }
}
