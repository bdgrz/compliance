using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A class of restricted fields that is read only with its own tenant permission.</summary>
public sealed record FieldClass(string Name)
{
    public string ReadPermission => $"field.{Name}.read";
}

/// <summary>
///     Field classes known to the authorization model. Workforce classes are added when M0-D06 confirms
///     them; evidence classes follow M0-D16.
/// </summary>
public static class FieldClasses
{
    /// <summary>Evidence content held in quarantine; readable only by an Org Admin (M0-D16).</summary>
    public static FieldClass EvidenceQuarantinedContent { get; } = new("evidence.quarantined_content");
}

/// <summary>A restricted field value, or an explicit redaction marker when the actor lacks its class.</summary>
public readonly record struct RestrictedField<T>(T? Value, bool IsRedacted);

/// <summary>Applies field-class read permissions before a handler returns, lists, searches, or exports a record.</summary>
public static class FieldRestrictions
{
    public static async ValueTask<RestrictedField<T>> ReadAsync<T>(IPermissionAuthorizer permissions,
        Uuid tenantId, Uuid memberId, FieldClass fieldClass, T value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(fieldClass);
        return await permissions.IsAllowedAsync(tenantId, memberId, fieldClass.ReadPermission, ct)
            .ConfigureAwait(false)
            ? new RestrictedField<T>(value, IsRedacted: false)
            : new RestrictedField<T>(default, IsRedacted: true);
    }
}
