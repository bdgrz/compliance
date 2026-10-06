using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportPaging
{
    const string Prefix = "application_import_v1:";

    public static RequestError? Decode(Uuid tenantId, Uuid batchId, long revision,
        string? cursor, out string? rowsCursor)
    {
        rowsCursor = cursor;
        if (cursor is null)
            return null;
        if (cursor.Length > 8192)
            return InvalidCursor();
        if (!cursor.StartsWith(Prefix, StringComparison.Ordinal))
            return null;
        var parts = cursor[Prefix.Length..].Split(':', 4);
        if (parts.Length != 4 || parts[0] != tenantId.ToString() || parts[1] != batchId.ToString() ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var cursorRevision) ||
            cursorRevision < 1 || string.IsNullOrWhiteSpace(parts[3]))
            return InvalidCursor();
        if (cursorRevision != revision)
            return ChangedPreview();
        rowsCursor = parts[3];
        return null;
    }

    public static string? Encode(Uuid tenantId, Uuid batchId, long revision, string? rowsCursor) =>
        rowsCursor is null ? null : $"{Prefix}{tenantId}:{batchId}:{revision.ToString(CultureInfo.InvariantCulture)}:{rowsCursor}";

    // Check legacy revision only after the indexed query has validated the cursor's format and scope.
    public static RequestError? CheckLegacyRevision(long revision, string? cursor) =>
        revision != 1 && cursor is not null && !cursor.StartsWith(Prefix, StringComparison.Ordinal)
            ? ChangedPreview() : null;

    static RequestError InvalidCursor() => new(RequestErrorKind.Validation, "The import cursor is invalid.");

    static RequestError ChangedPreview() => new(RequestErrorKind.Conflict,
        "The import lifecycle changed; restart paging.", isTransient: true);
}
