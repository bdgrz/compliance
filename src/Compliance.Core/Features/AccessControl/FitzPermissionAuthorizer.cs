using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzPermissionAuthorizer(IKvClient client) : IPermissionAuthorizer
{
    public async ValueTask<bool> IsAllowedAsync(
        Uuid tenantId,
        Uuid memberId,
        string permission,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty || string.IsNullOrWhiteSpace(permission))
            return false;

        await using var transaction = await client.BeginAsync(
            Route(tenantId.ToString()),
            KvDurability.Async,
            KvMode.ReadOnly,
            ct).ConfigureAwait(false);
        var grant = await transaction.GetAsync(PermissionProjectionKeys.Grant(memberId, permission), ct)
            .ConfigureAwait(false);
        return grant.Found;
    }

    internal static string Route(string tenantId) => $"kv://bdgrz/permissions/{tenantId}";
}
