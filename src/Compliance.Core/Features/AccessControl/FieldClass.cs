namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A class of restricted fields that is read only with its own tenant permission.</summary>
public sealed record FieldClass(string Name)
{
    public string ReadPermission => $"field.{Name}.read";
}
