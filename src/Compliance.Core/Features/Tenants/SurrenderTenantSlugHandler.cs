using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class SurrenderTenantSlugHandler(IAggregateRepository repository)
    : IRequestHandler<SurrenderTenantSlug>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<SurrenderTenantSlug> context, CancellationToken ct)
    {
        var slug = await repository.HydrateAsync(new TenantSlug(context.Request.Slug), ct);
        var version = slug.Version;
        var result = slug.Surrender(context.Request.TenantId);
        if (result.IsSuccess && slug.Version != version)
            await repository.SaveAsync(slug, context, ct);
        return result;
    }
}
