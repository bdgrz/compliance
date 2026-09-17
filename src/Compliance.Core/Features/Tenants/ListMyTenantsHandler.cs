using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListMyTenantsHandler(
    ITenantDirectory activeTenants,
    ITenantMembershipDirectoryReader memberships,
    ITenantDirectoryReader tenants) : IRequestHandler<ListMyTenants, Page<TenantMembershipSummary>>
{
    const int DefaultLimit = 200;
    const int MaxLimit = 200;

    public async ValueTask<Result<Page<TenantMembershipSummary>>> HandleAsync(
        IRequestContext<ListMyTenants> context,
        CancellationToken ct)
    {
        // ListMyTenantsAuthorizer guarantees this before the handler ever runs.
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "ListMyTenantsAuthorizer must reject requests without a Bdgrz user identity.");

        var limit = Math.Clamp(context.Request.Limit ?? DefaultLimit, 1, MaxLimit);
        var items = new List<TenantMembershipSummary>();
        await foreach (var tenantId in activeTenants.GetActiveTenantsAsync(ct))
        {
            if (items.Count >= limit)
            {
                break;
            }

            if (!await memberships.IsMemberAsync(tenantId.Value, userId, ct))
            {
                continue;
            }

            // A membership row can briefly outrun the tenant directory's own projector; skip rather
            // than fail — it self-heals once TenantDirectory catches up.
            var tenant = await tenants.GetAsync(Uuid.Parse(tenantId.Value, CultureInfo.InvariantCulture), ct);
            if (tenant is not null)
            {
                items.Add(new TenantMembershipSummary(tenant.TenantId, tenant.Name, tenant.Slug));
            }
        }

        return Result<Page<TenantMembershipSummary>>.Success(new Page<TenantMembershipSummary>(items, null));
    }
}
