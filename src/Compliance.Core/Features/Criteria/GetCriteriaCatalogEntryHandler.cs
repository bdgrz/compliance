using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

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
