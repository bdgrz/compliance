using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportPaging
{
    const string SourcePrefix = "application_import_v2:";
    const string Prefix = "application_import_v1:";

    public static RequestError? Decode(Uuid tenantId, Uuid batchId, long revision,
        string? cursor, out string? rowsCursor, ulong? sourcePosition = null)
    {
        rowsCursor = cursor;
        if (cursor is null)
            return null;
        if (cursor.Length > 8192)
            return InvalidCursor();
        if (cursor.StartsWith(SourcePrefix, StringComparison.Ordinal))
        {
            var sourceParts = cursor[SourcePrefix.Length..].Split(':', 5);
            if (sourceParts.Length != 5 || sourceParts[0] != tenantId.ToString() || sourceParts[1] != batchId.ToString() ||
                !long.TryParse(sourceParts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var batchRevision) ||
                batchRevision < 1 || !ulong.TryParse(sourceParts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var position) ||
                string.IsNullOrWhiteSpace(sourceParts[4]))
                return InvalidCursor();
            if (batchRevision != revision || (sourcePosition is { } wanted && position != wanted))
                return ChangedPreview();
            rowsCursor = sourceParts[4];
            return null;
        }
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

    public static string? Encode(Uuid tenantId, Uuid batchId, long revision, string? rowsCursor, ulong? sourcePosition = null) =>
        rowsCursor is null ? null : sourcePosition is { } position
            ? $"{SourcePrefix}{tenantId}:{batchId}:{revision.ToString(CultureInfo.InvariantCulture)}:{position.ToString(CultureInfo.InvariantCulture)}:{rowsCursor}"
            : $"{Prefix}{tenantId}:{batchId}:{revision.ToString(CultureInfo.InvariantCulture)}:{rowsCursor}";

    // Check legacy revision only after the indexed query has validated the cursor's format and scope.
    public static RequestError? CheckLegacyRevision(long revision, string? cursor, ulong? sourcePosition = null) =>
        cursor is not null && !cursor.StartsWith(SourcePrefix, StringComparison.Ordinal) &&
        ((sourcePosition is > 0) || (revision != 1 && !cursor.StartsWith(Prefix, StringComparison.Ordinal)))
            ? ChangedPreview() : null;

    static RequestError InvalidCursor() => new(RequestErrorKind.Validation, "The import cursor is invalid.");

    static RequestError ChangedPreview() => new(RequestErrorKind.Conflict,
        "The import lifecycle changed; restart paging.", isTransient: true);
}
