using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

sealed class AlwaysMemberDirectory : ITenantMembershipDirectoryReader
{
    public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
        ValueTask.FromResult(true);

    public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
}
