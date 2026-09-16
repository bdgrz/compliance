namespace Bdgrz.Compliance.Features.AccessControl;

static class Permissions
{
    public static string Normalize(string permission)
    {
        var normalized = permission.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            throw new ArgumentException("A permission is required.", nameof(permission));
        return normalized;
    }
}
