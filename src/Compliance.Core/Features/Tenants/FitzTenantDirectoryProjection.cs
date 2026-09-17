using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class FitzTenantDirectoryProjection(IKvClient client)
    : FitzKvProjectionStore(client, TenantDirectoryKeys.Route()), ITenantDirectoryProjection
{
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
}
