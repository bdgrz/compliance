using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationRelationshipPaging
{
    const string CursorPrefix = "application_relationship_v2:";

    public static RequestError? Create(Uuid tenantId, Uuid applicationId, Uuid actorId,
        long relationshipRevision, IReadOnlyList<ApplicationRelationshipView> items,
        string fingerprint, int? requestedLimit, string? cursor,
        out ApplicationRelationshipPage page)
    {
        page = null!;
        if (requestedLimit is < 1 or > 200)
            return new RequestError(RequestErrorKind.Validation,
                "The application relationship list limit must be between 1 and 200.");
        var limit = requestedLimit ?? 50;
        var offset = 0;
        if (cursor is not null)
        {
            var error = ReadCursor(tenantId, applicationId, actorId, limit,
                relationshipRevision, fingerprint, cursor, out offset);
            if (error is not null)
                return error;
        }
        if (offset > items.Count)
            return InvalidCursor();

        var visible = items.Skip(offset).Take(limit).ToArray();
        var nextOffset = offset + visible.Length;
        var nextCursor = nextOffset < items.Count
            ? WriteCursor(tenantId, applicationId, actorId, limit, nextOffset,
                relationshipRevision, fingerprint)
            : null;
        page = new ApplicationRelationshipPage(relationshipRevision, visible, nextCursor);
        return null;
    }

    static RequestError? ReadCursor(Uuid tenantId, Uuid applicationId, Uuid actorId,
        int limit, long relationshipRevision, string fingerprint, string cursor,
        out int offset)
    {
        offset = 0;
        if (cursor.Length > 1024 || !cursor.StartsWith(CursorPrefix, StringComparison.Ordinal))
            return InvalidCursor();
        try
        {
            var encoded = cursor[CursorPrefix.Length..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var payload = JsonSerializer.Deserialize<RelationshipCursorPayload>(json);
            if (payload is null || payload.TenantId != tenantId.ToString() ||
                payload.ApplicationId != applicationId.ToString() ||
                payload.ActorId != actorId.ToString() || payload.Limit != limit ||
                payload.Offset < 0 || payload.VisibleRelationshipRevision < 0)
                return InvalidCursor();
            offset = payload.Offset;
            if (payload.VisibleRelationshipRevision != relationshipRevision ||
                !string.Equals(payload.VisibleRowsFingerprint, fingerprint,
                    StringComparison.Ordinal))
                return new RequestError(RequestErrorKind.Conflict,
                    "The visible application relationships changed; restart paging.",
                    isTransient: true);
            return null;
        }
        catch (FormatException)
        {
            return InvalidCursor();
        }
        catch (JsonException)
        {
            return InvalidCursor();
        }
    }

    static string WriteCursor(Uuid tenantId, Uuid applicationId, Uuid actorId,
        int limit, int offset, long relationshipRevision, string fingerprint)
    {
        var payload = new RelationshipCursorPayload(tenantId.ToString(),
            applicationId.ToString(), actorId.ToString(), limit, offset,
            relationshipRevision, fingerprint);
        return CursorPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(payload)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    static RequestError InvalidCursor() => new(RequestErrorKind.Validation,
        "The application relationship cursor is invalid.");

    sealed record RelationshipCursorPayload(string TenantId, string ApplicationId,
        string ActorId, int Limit, int Offset, long VisibleRelationshipRevision,
        string VisibleRowsFingerprint);
}
