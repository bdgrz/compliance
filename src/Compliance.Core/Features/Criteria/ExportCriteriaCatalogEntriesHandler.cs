using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class ExportCriteriaCatalogEntriesHandler(ICriteriaCatalog catalog,
    ICriteriaTextOverlayReader overlays)
    : IRequestHandler<ExportCriteriaCatalogEntries, Page<Criterion>>
{
    public ValueTask<Result<Page<Criterion>>> HandleAsync(
        IRequestContext<ExportCriteriaCatalogEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        return CriteriaCatalogEntriesPage.ReadAsync(catalog, overlays, request.TenantId,
            request.EditionId, request.Category, request.Kind, request.ParentIdentifier,
            request.Limit, request.Cursor, CriteriaTextOverlayPurpose.Export, ct);
    }
}
