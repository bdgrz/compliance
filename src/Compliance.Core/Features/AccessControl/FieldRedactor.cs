namespace Bdgrz.Compliance.Features.AccessControl;

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
