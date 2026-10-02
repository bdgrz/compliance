using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed class GetCriteriaCatalogEntryHandler(ICriteriaCatalog catalog,
    CriteriaTextOverlayReader overlays)
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
        if (!CriteriaTextOverlayPurpose.TryGetExport(request.Purpose, out var export))
            return Result<Criterion>.Failure(new RequestError(RequestErrorKind.Validation,
                "Criteria purpose must be display or export."));
        return Result<Criterion>.Success(await overlays.ApplyAsync(request.TenantId, entry,
            export, ct).ConfigureAwait(false));
    }
}
