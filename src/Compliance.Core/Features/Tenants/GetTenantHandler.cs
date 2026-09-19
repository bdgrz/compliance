using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class GetTenantHandler(ITenantDirectoryReader directory) : IRequestHandler<GetTenant, TenantView>
{
    public async ValueTask<Result<TenantView>> HandleAsync(IRequestContext<GetTenant> context,
        CancellationToken ct)
    {
        var tenant = await directory.GetAsync(context.Request.TenantId, ct).ConfigureAwait(false);
        return tenant is null
            ? Result<TenantView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."))
            : Result<TenantView>.Success(tenant);
    }
}
