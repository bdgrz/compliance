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
                new TenantView(registered.TenantId, registered.Name, registered.Slug,
                    Status: "provisioning",
                    LegalName: registered.LegalName ?? registered.Name,
                    OperatorUserId: registered.CreatorIsAdministrator ? null : registered.OwnerUserId,
                    RequiresInvitation: registered.FirstAdministratorEmail is not null &&
                                        !registered.CreatorIsAdministrator,
                    RequiresActivation: registered.CreatorIsAdministrator ||
                                        registered.FirstAdministratorEmail is not null),
                ct).ConfigureAwait(false);
        }
        else
        {
            if (domainEvent is TenantSlugChanged changed)
            {
                var old = await TenantDirectorySchema.Directory.GetAsync(Transaction, changed.TenantId, ct)
                    .ConfigureAwait(false);
                if (old is not null)
                    await TenantDirectorySchema.Directory.ReplaceAsync(Transaction, old,
                        old with { Slug = changed.NewSlug }, ct).ConfigureAwait(false);
                return;
            }
            var (tenantId, status) = domainEvent switch
            {
                TenantSlugConfirmed ev => (ev.TenantId, "active"),
                TenantSlugRejected ev => (ev.TenantId, "rejected"),
                TenantSuspended ev => (ev.TenantId, "suspended"),
                TenantReactivated ev => (ev.TenantId, "active"),
                TenantActivated ev => (ev.TenantId, "active"),
                _ => (Uuid.Empty, string.Empty),
            };
            if (tenantId != Uuid.Empty)
            {
                var current = await TenantDirectorySchema.Directory.GetAsync(Transaction, tenantId, ct)
                    .ConfigureAwait(false);
                if (current is not null)
                {
                    if (domainEvent is TenantSlugConfirmed && current.RequiresActivation)
                        status = "provisioning";
                    if (domainEvent is TenantActivated && current.Status == "suspended")
                        status = "suspended";
                    await TenantDirectorySchema.Directory.ReplaceAsync(
                        Transaction, current, current with { Status = status }, ct).ConfigureAwait(false);
                }
            }
        }
    }

    public async ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await TenantDirectorySchema.Directory.GetAsync(tx, tenantId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await TenantDirectorySchema.Directory.QueryPrimaryAsync(tx, limit, cursor, ct)
            .ConfigureAwait(false);
    }
}
