using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportVisibilityPaging
{
    const string Prefix = "application_visibility_v1:";

    public static string? Decode(Uuid tenantId, long epoch, string? cursor)
    {
        if (cursor is null)
            return null;
        if (cursor.Length > 8192)
            throw new KvDirectoryQueryException();
        if (!cursor.StartsWith(Prefix, StringComparison.Ordinal))
            return epoch == 0 ? cursor : throw new ApplicationImportVisibilityChangedException();
        var parts = cursor[Prefix.Length..].Split(':', 3);
        if (parts.Length != 3 || parts[0] != tenantId.ToString() ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var cursorEpoch) ||
            cursorEpoch < 0 || string.IsNullOrWhiteSpace(parts[2]))
            throw new KvDirectoryQueryException();
        return cursorEpoch == epoch ? parts[2] : throw new ApplicationImportVisibilityChangedException();
    }

    public static string? Encode(Uuid tenantId, long epoch, string? cursor) =>
        cursor is null || epoch == 0 ? cursor : $"{Prefix}{tenantId}:{epoch.ToString(CultureInfo.InvariantCulture)}:{cursor}";
}
