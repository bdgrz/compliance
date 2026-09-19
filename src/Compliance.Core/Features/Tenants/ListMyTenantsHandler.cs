using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListMyTenantsHandler(
    ITenantDirectory activeTenants,
    ITenantMembershipDirectoryReader memberships,
    ITenantDirectoryReader tenants) : IRequestHandler<ListMyTenants, Page<TenantMembershipSummary>>
{
    const int DefaultLimit = 50;
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

        var request = context.Request;
        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

        // GetActiveTenantsAsync has no ordering guarantee (it folds a HashSet), so the scan itself
        // has to sort lexicographically by tenant ID before a "resume after this ID" cursor means
        // anything — same convention KvDirectory cursors already use everywhere else.
        var ordered = new List<TenantId>();
        await foreach (var tenantId in activeTenants.GetActiveTenantsAsync(ct))
        {
            ordered.Add(tenantId);
        }

        ordered.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));

        var items = new List<TenantMembershipSummary>();
        string? nextCursor = null;
        for (var index = 0; index < ordered.Count; index++)
        {
            var tenantId = ordered[index];
            if (request.Cursor is not null && string.CompareOrdinal(tenantId.Value, request.Cursor) <= 0)
            {
                continue;
            }

            if (items.Count >= limit)
            {
                nextCursor = ordered[index - 1].Value;
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
                items.Add(new TenantMembershipSummary(tenant.TenantId, tenant.Name, tenant.Slug,
                    tenant.Status));
            }
        }

        return Result<Page<TenantMembershipSummary>>.Success(new Page<TenantMembershipSummary>(items, nextCursor));
    }
}
