using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantSlugHandler(IAggregateRepository repository)
    : IRequestHandler<RegisterTenantSlug>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RegisterTenantSlug> context, CancellationToken ct)
    {
        var slug = await repository.HydrateAsync(new TenantSlug(context.Request.Slug), ct);
        var version = slug.Version;
        var result = slug.Register(context.Request.TenantId);
        if (result.IsSuccess && slug.Version != version)
            await repository.SaveAsync(slug, context, ct);
        return result;
    }
}
