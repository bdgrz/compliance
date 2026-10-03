using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaCatalogEntriesPage
{
    public static async ValueTask<Result<Page<Criterion>>> ReadAsync(ICriteriaCatalog catalog,
        CriteriaTextOverlayReader overlays, Uuid tenantId, Uuid editionId, string? category,
        string? kind, string? parentIdentifier, int? limit, string? cursor,
        CriteriaTextOverlayPurpose purpose, CancellationToken ct)
    {
        if (limit is < 1 or > 200 ||
            category is not null and not ("security" or "availability" or "confidentiality" or
                "processing_integrity" or "privacy") ||
            kind is not null and not ("criterion" or "point_of_focus"))
            return Failure(RequestErrorKind.Validation, "The criteria list filter or limit is invalid.");
        if (catalog.GetEdition(editionId) is null)
            return Failure(RequestErrorKind.NotFound, "The criteria edition was not found.");

        var start = 0;
        if (cursor is not null && !CriteriaPageCursor.TryRead(tenantId, editionId, category, kind,
                parentIdentifier, cursor, purpose, out start))
            return Failure(RequestErrorKind.Validation, "The criteria list cursor is invalid.");
        var entries = catalog.ListEntries(editionId, category, kind, parentIdentifier);
        if (start > entries.Count)
            return Failure(RequestErrorKind.Validation, "The criteria list cursor is invalid.");
        var page = entries.Skip(start).Take(limit ?? 50).ToArray();
        var export = purpose == CriteriaTextOverlayPurpose.Export;
        var items = await overlays.ApplyPageAsync(tenantId, page, export, ct).ConfigureAwait(false);
        var next = start + items.Count < entries.Count
            ? CriteriaPageCursor.Write(tenantId, editionId, category, kind, parentIdentifier,
                purpose, start + items.Count)
            : null;
        return Result<Page<Criterion>>.Success(new Page<Criterion>(items, next));
    }

    static Result<Page<Criterion>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<Criterion>>.Failure(new RequestError(kind, message));
}
