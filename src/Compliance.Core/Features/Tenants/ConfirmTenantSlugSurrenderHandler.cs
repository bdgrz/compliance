using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ConfirmTenantSlugSurrenderHandler(IAggregateRepository repository)
    : IRequestHandler<ConfirmTenantSlugSurrender>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ConfirmTenantSlugSurrender> context,
        CancellationToken ct)
    {
        var tenant = await repository.HydrateAsync(new Tenant(context.Request.TenantId), ct);
        var result = tenant.ConfirmSlugSurrender(context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
