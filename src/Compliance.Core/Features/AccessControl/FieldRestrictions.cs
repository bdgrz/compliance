using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Applies field-class read permissions before a handler returns, lists, searches, or exports a record.</summary>
public static class FieldRestrictions
{
    public static async ValueTask<FieldRedactor> ForActorAsync(IPermissionAuthorizer permissions,
        Uuid tenantId, Uuid userId, FieldClass fieldClass, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(fieldClass);
        var canRead = await permissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), fieldClass.ReadPermission, ct)
            .ConfigureAwait(false);
        return new FieldRedactor(fieldClass, canRead);
    }
}
