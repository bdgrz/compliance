using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class GetCriteriaCatalogEntryHandler(ICriteriaCatalog catalog,
    ICriteriaTextOverlayReader overlays)
    : IRequestHandler<GetCriteriaCatalogEntry, Criterion>
{
    public async ValueTask<Result<Criterion>> HandleAsync(IRequestContext<GetCriteriaCatalogEntry> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var entry = catalog.GetEntry(request.EditionId, request.Identifier);
        if (entry is null)
            return Result<Criterion>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion was not found."));
        return await overlays.ApplyAsync(request.TenantId, entry, isExport: false, ct)
            .ConfigureAwait(false);
    }
}
