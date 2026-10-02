using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class ListCriteriaCatalogEntriesHandler(ICriteriaCatalog catalog,
    CriteriaTextOverlayReader overlays)
    : IRequestHandler<ListCriteriaCatalogEntries, Page<Criterion>>
{
    public async ValueTask<Result<Page<Criterion>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 ||
            request.Category is not null and not ("security" or "availability" or
                "confidentiality" or "processing_integrity" or "privacy") ||
            request.Kind is not null and not ("criterion" or "point_of_focus"))
            return Failure(RequestErrorKind.Validation, "The criteria list filter or limit is invalid.");
        if (!CriteriaTextOverlayPurpose.TryGetExport(request.Purpose, out var export))
            return Failure(RequestErrorKind.Validation, "Criteria purpose must be display or export.");
        if (catalog.GetEdition(request.EditionId) is null)
            return Failure(RequestErrorKind.NotFound, "The criteria edition was not found.");

        var start = 0;
        if (request.Cursor is not null && !CriteriaPageCursor.TryRead(request, out start))
            return Failure(RequestErrorKind.Validation, "The criteria list cursor is invalid.");
        var entries = catalog.ListEntries(request.EditionId, request.Category, request.Kind,
            request.ParentIdentifier);
        if (start > entries.Count)
            return Failure(RequestErrorKind.Validation, "The criteria list cursor is invalid.");
        var limit = request.Limit ?? 50;
        var page = entries.Skip(start).Take(limit).ToArray();
        var items = await overlays.ApplyPageAsync(request.TenantId, page, export, ct)
            .ConfigureAwait(false);
        var next = start + items.Count < entries.Count
            ? CriteriaPageCursor.Write(request, start + items.Count)
            : null;
        return Result<Page<Criterion>>.Success(new Page<Criterion>(items, next));
    }

    static Result<Page<Criterion>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<Criterion>>.Failure(new RequestError(kind, message));
}
