using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RejectTenantSlugSurrenderHandler(IAggregateRepository repository)
    : IRequestHandler<RejectTenantSlugSurrender>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RejectTenantSlugSurrender> context,
        CancellationToken ct)
    {
        var tenant = await repository.HydrateAsync(new Tenant(context.Request.TenantId), ct);
        var result = tenant.RejectSlugSurrender(context.Request.Slug);
        if (result.IsSuccess)
            await repository.SaveAsync(tenant, context, ct);
        return result;
    }
}
