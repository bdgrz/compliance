using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ConfirmTenantSlugHandler(IAggregateRepository repository) : IRequestHandler<ConfirmTenantSlug>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ConfirmTenantSlug> context, CancellationToken ct)
    {
        var tenant = await repository.HydrateAsync(new Tenant(context.Request.TenantId), ct);
        var result = tenant.ConfirmSlug(context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
