using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RejectTenantSlugHandler(IAggregateRepository repository) : IRequestHandler<RejectTenantSlug>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RejectTenantSlug> context, CancellationToken ct)
    {
        var tenant = await repository.HydrateAsync(new Tenant(context.Request.TenantId), ct);
        var result = tenant.RejectSlug(context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
