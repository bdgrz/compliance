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
