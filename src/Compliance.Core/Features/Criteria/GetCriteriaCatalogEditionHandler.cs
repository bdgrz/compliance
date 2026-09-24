using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

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
