using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ResolveMyTenantSlugHandler(IAggregateReader reader,
    ITenantMembershipDirectoryReader memberships) : IRequestHandler<ResolveMyTenantSlug, TenantSlugResolution>
{
    public async ValueTask<Result<TenantSlugResolution>> HandleAsync(
        IRequestContext<ResolveMyTenantSlug> context, CancellationToken ct)
    {
        if (!TenantSlugs.TryNormalize(context.Request.Slug, out var slug))
            return NotFound();
        var actorId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId)
            ? userId
            : throw new InvalidOperationException("ResolveMyTenantSlugAuthorizer must reject this actor.");
        var reservation = await reader.HydrateAsync(new TenantSlug(slug), ct).ConfigureAwait(false);
        if (reservation.OwningTenantId is not { } tenantId ||
            !await memberships.IsMemberAsync(tenantId.ToString(), actorId, ct).ConfigureAwait(false))
            return NotFound();

        var tenant = await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        if (!tenant.IsActive || tenant.CurrentSlug is not { } currentSlug)
            return NotFound();
        return Result<TenantSlugResolution>.Success(
            new TenantSlugResolution(tenantId, currentSlug, currentSlug != slug));
    }

    static Result<TenantSlugResolution> NotFound() =>
        Result<TenantSlugResolution>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The organization was not found."));
}
