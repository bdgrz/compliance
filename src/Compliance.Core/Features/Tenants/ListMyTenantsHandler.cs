using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListMyTenantsHandler(
    ITenantMembershipDirectoryReader memberships,
    ITenantDirectoryReader tenants) : IRequestHandler<ListMyTenants, Page<TenantMembershipSummary>>
{
    public async ValueTask<Result<Page<TenantMembershipSummary>>> HandleAsync(
        IRequestContext<ListMyTenants> context,
        CancellationToken ct)
    {
        // ListMyTenantsAuthorizer guarantees this before the handler ever runs.
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException(
                "ListMyTenantsAuthorizer must reject requests without a Bdgrz user identity.");

        var request = context.Request;
        var page = await memberships.ListByUserAsync(
            userId, request.Limit, request.Cursor, ListRequestNormalization.IsDescending(request.Sort), ct);

        var items = new List<TenantMembershipSummary>(page.Items.Count);
        foreach (var membership in page.Items)
        {
            var tenant = await tenants.GetAsync(membership.TenantId, ct);
            // A membership row can briefly outrun the tenant directory's own projector; skip rather
            // than fail — it self-heals once TenantDirectory catches up.
            if (tenant is not null)
            {
                items.Add(new TenantMembershipSummary(tenant.TenantId, tenant.Name, tenant.Slug));
            }
        }

        return Result<Page<TenantMembershipSummary>>.Success(new Page<TenantMembershipSummary>(items, page.NextCursor));
    }
}
