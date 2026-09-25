namespace Bdgrz.Compliance.Features.AccessControl;

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
