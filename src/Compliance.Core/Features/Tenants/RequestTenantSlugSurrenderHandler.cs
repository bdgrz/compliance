using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RequestTenantSlugSurrenderHandler(IAggregateRepository repository)
    : IRequestHandler<RequestTenantSlugSurrender>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RequestTenantSlugSurrender> context,
        CancellationToken ct)
    {
        var tenant = await repository.HydrateAsync(new Tenant(context.Request.TenantId), ct);
        var result = tenant.RequestSlugSurrender(context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
