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

/// <summary>
///     A restricted field value, or an explicit redaction. A redacted field holds no value, so reading
///     <see cref="Value" /> fails instead of yielding a default that could pass for data.
/// </summary>
public sealed class RestrictedField<T>
{
    readonly T _value;

    RestrictedField(T value, bool isRedacted)
    {
        _value = value;
        IsRedacted = isRedacted;
    }

    internal static RestrictedField<T> Redacted { get; } = new(default!, isRedacted: true);

    public bool IsRedacted { get; }

    public T Value => IsRedacted
        ? throw new InvalidOperationException("The field is redacted for this actor.")
        : _value;

    internal static RestrictedField<T> Visible(T value) => new(value, isRedacted: false);
}

/// <summary>One actor's read decision for one field class, resolved once and applied to many values.</summary>
public sealed class FieldRedactor
{
    internal FieldRedactor(FieldClass fieldClass, bool canRead)
    {
        FieldClass = fieldClass;
        CanRead = canRead;
    }

    public FieldClass FieldClass { get; }

    public bool CanRead { get; }

    public RestrictedField<T> Apply<T>(T value) =>
        CanRead ? RestrictedField<T>.Visible(value) : RestrictedField<T>.Redacted;
}

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
