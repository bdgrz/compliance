using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportMissingPaging
{
    const string Prefix = "application_import_missing_v1:";

    public static Result<int> Decode(Uuid tenantId, Uuid batchId, long revision,
        ulong sourcePosition, string? cursor)
    {
        if (cursor is null)
            return Result<int>.Success(0);
        if (cursor.Length > 256 || !cursor.StartsWith(Prefix, StringComparison.Ordinal))
            return Invalid();
        var parts = cursor[Prefix.Length..].Split(':');
        if (parts.Length != 5 || parts[0] != tenantId.ToString() || parts[1] != batchId.ToString() ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var batchRevision) ||
            batchRevision < 1 || !ulong.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var position) ||
            !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 1)
            return Invalid();
        return batchRevision != revision || position != sourcePosition
            ? Result<int>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import source changed; restart missing-row paging.", isTransient: true))
            : Result<int>.Success(offset);
    }

    public static string Encode(Uuid tenantId, Uuid batchId, long revision, ulong sourcePosition, int offset) =>
        $"{Prefix}{tenantId}:{batchId}:{revision.ToString(CultureInfo.InvariantCulture)}:{sourcePosition.ToString(CultureInfo.InvariantCulture)}:{offset.ToString(CultureInfo.InvariantCulture)}";

    static Result<int> Invalid() => Result<int>.Failure(new RequestError(RequestErrorKind.Validation,
        "The missing-row preview cursor is invalid."));
}
