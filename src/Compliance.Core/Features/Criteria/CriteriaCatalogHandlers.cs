using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class ListCriteriaCatalogEditionsHandler(ICriteriaCatalog catalog)
    : IRequestHandler<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>
{
    public ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEditions> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<IReadOnlyList<CriteriaCatalogEdition>>.Success(catalog.Editions));
}

public sealed class GetCriteriaCatalogEditionHandler(ICriteriaCatalog catalog)
    : IRequestHandler<GetCriteriaCatalogEdition, CriteriaCatalogEdition>
{
    public ValueTask<Result<CriteriaCatalogEdition>> HandleAsync(
        IRequestContext<GetCriteriaCatalogEdition> context, CancellationToken ct)
    {
        var edition = catalog.GetEdition(context.Request.EditionId);
        return ValueTask.FromResult(edition is null
            ? Result<CriteriaCatalogEdition>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criteria edition was not found."))
            : Result<CriteriaCatalogEdition>.Success(edition));
    }
}

public sealed class GetCriteriaCatalogEntryHandler(ICriteriaCatalog catalog)
    : IRequestHandler<GetCriteriaCatalogEntry, Criterion>
{
    public ValueTask<Result<Criterion>> HandleAsync(IRequestContext<GetCriteriaCatalogEntry> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var entry = catalog.GetEntry(request.EditionId, request.Identifier);
        return ValueTask.FromResult(entry is null
            ? Result<Criterion>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion was not found."))
            : Result<Criterion>.Success(entry));
    }
}

public sealed class ListCriteriaCatalogEntriesHandler(ICriteriaCatalog catalog)
    : IRequestHandler<ListCriteriaCatalogEntries, Page<Criterion>>
{
    public ValueTask<Result<Page<Criterion>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 ||
            request.Category is not null and not ("security" or "availability" or
                "confidentiality" or "processing_integrity" or "privacy") ||
            request.Kind is not null and not ("criterion" or "point_of_focus"))
            return Failure(RequestErrorKind.Validation, "The criteria list filter or limit is invalid.");
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
        var items = entries.Skip(start).Take(limit).ToArray();
        var next = start + items.Length < entries.Count
            ? CriteriaPageCursor.Write(request, start + items.Length)
            : null;
        return ValueTask.FromResult(Result<Page<Criterion>>.Success(new Page<Criterion>(items, next)));
    }

    static ValueTask<Result<Page<Criterion>>> Failure(RequestErrorKind kind, string message) =>
        ValueTask.FromResult(Result<Page<Criterion>>.Failure(new RequestError(kind, message)));
}

static class CriteriaPageCursor
{
    public static string Write(ListCriteriaCatalogEntries request, int nextIndex)
    {
        var content = string.Join('\n', request.TenantId.ToString(), request.EditionId.ToString(),
            request.Category ?? "", request.Kind ?? "", request.ParentIdentifier ?? "",
            nextIndex.ToString(CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryRead(ListCriteriaCatalogEntries request, out int index)
    {
        index = 0;
        try
        {
            var cursor = request.Cursor!;
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('\n');
            return parts.Length == 6 &&
                   parts[0] == request.TenantId.ToString() &&
                   parts[1] == request.EditionId.ToString() &&
                   parts[2] == (request.Category ?? "") &&
                   parts[3] == (request.Kind ?? "") &&
                   parts[4] == (request.ParentIdentifier ?? "") &&
                   int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture,
                       out index) && index >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
