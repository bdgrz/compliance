using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantsHandler(ITenantDirectoryReader directory)
    : IRequestHandler<ListTenants, Page<TenantView>>
{
    public async ValueTask<Result<Page<TenantView>>> HandleAsync(
        IRequestContext<ListTenants> context, CancellationToken ct) =>
        Result<Page<TenantView>>.Success(await directory.ListAsync(
            Math.Clamp(context.Request.Limit ?? 50, 1, 200), context.Request.Cursor, ct)
            .ConfigureAwait(false));
}
