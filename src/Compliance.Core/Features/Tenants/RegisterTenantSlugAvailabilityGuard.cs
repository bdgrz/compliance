using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Rejects an obviously-occupied slug before a tenant is created, so the RBAC bootstrap and
///     owner-registration reactors <see cref="Tenant" /> triggers on <c>TenantRegistered</c> never
///     fire for a registration the slug claim would reject anyway. Not authoritative: the slug can
///     still be claimed by another registration between this read and
///     <see cref="TenantSlug.Register" />, which is what actually decides ownership and drives
///     <see cref="Tenant" /> through its pending/confirmed/rejected slug states.
/// </summary>
sealed class RegisterTenantSlugAvailabilityGuard(IAggregateReader reader) : IRequestGuard<RegisterTenant>
{
    public async ValueTask<Result> GuardAsync(IRequestContext<RegisterTenant> context, CancellationToken ct)
    {
        if (!TenantSlugs.TryNormalize(context.Request.Slug, out var slug))
            return Result.Success; // Tenant.Register produces the authoritative validation error.

        var tenantSlug = await reader.HydrateAsync(new TenantSlug(slug), ct).ConfigureAwait(false);
        return tenantSlug.IsAvailableFor(context.RequestId)
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "That tenant slug is already registered."));
    }
}
