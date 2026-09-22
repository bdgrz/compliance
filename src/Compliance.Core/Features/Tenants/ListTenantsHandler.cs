using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantsHandler(ITenantDirectoryReader directory)
    : IRequestHandler<ListTenants, Page<TenantView>>
{
    public async ValueTask<Result<Page<TenantView>>> HandleAsync(
        IRequestContext<ListTenants> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TenantView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform tenant list limit must be between 1 and 200."));
        try
        {
            return Result<Page<TenantView>>.Success(await directory.ListAsync(request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TenantView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform tenant cursor is invalid."));
        }
    }
}
