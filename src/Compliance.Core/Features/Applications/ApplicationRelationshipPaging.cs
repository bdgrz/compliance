using System.Globalization;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationRelationshipPaging
{
    const string CursorPrefix = "application_relationship_v1:";

    public static RequestError? Create(Uuid tenantId, Uuid applicationId,
        long relationshipRevision, IReadOnlyList<ApplicationRelationshipView> items,
        int? requestedLimit, string? cursor, out ApplicationRelationshipPage page)
    {
        page = null!;
        if (requestedLimit is < 1 or > 200)
            return new RequestError(RequestErrorKind.Validation,
                "The application relationship list limit must be between 1 and 200.");
        var limit = requestedLimit ?? 50;
        var offset = 0;
        if (cursor is not null)
        {
            var error = ReadCursor(tenantId, applicationId, relationshipRevision, cursor,
                out offset);
            if (error is not null)
                return error;
        }
        if (offset > items.Count)
            return InvalidCursor();

        var visible = items.Skip(offset).Take(limit).ToArray();
        var nextOffset = offset + visible.Length;
        var nextCursor = nextOffset < items.Count
            ? WriteCursor(tenantId, applicationId, relationshipRevision, nextOffset)
            : null;
        page = new ApplicationRelationshipPage(relationshipRevision, visible, nextCursor);
        return null;
    }

    static RequestError? ReadCursor(Uuid tenantId, Uuid applicationId,
        long relationshipRevision, string cursor, out int offset)
    {
        offset = 0;
        if (cursor.Length > 1024 || !cursor.StartsWith(CursorPrefix, StringComparison.Ordinal))
            return InvalidCursor();
        try
        {
            var encoded = cursor[CursorPrefix.Length..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split('|');
            if (parts.Length != 4 ||
                !Uuid.TryParse(parts[0], CultureInfo.InvariantCulture, out var cursorTenantId) ||
                !Uuid.TryParse(parts[1], CultureInfo.InvariantCulture, out var cursorApplicationId) ||
                !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var cursorRevision) || cursorRevision < 0 ||
                !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out offset) ||
                offset < 0 || cursorTenantId != tenantId || cursorApplicationId != applicationId)
                return InvalidCursor();
            if (cursorRevision != relationshipRevision)
                return new RequestError(RequestErrorKind.Conflict,
                    "The application relationships changed; restart paging.", isTransient: true);
            return null;
        }
        catch (FormatException)
        {
            return InvalidCursor();
        }
    }

    static string WriteCursor(Uuid tenantId, Uuid applicationId,
        long relationshipRevision, int offset)
    {
        var payload = string.Join('|', tenantId.ToString(), applicationId.ToString(),
            relationshipRevision.ToString(CultureInfo.InvariantCulture),
            offset.ToString(CultureInfo.InvariantCulture));
        return CursorPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    static RequestError InvalidCursor() => new(RequestErrorKind.Validation,
        "The application relationship cursor is invalid.");
}
