using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Field policy for sensitive report content: organization-wide provider authoring authority
///     reads it in full; other authorized readers keep the shareable conclusions and counts.
/// </summary>
public sealed class AssuranceDisclosure(IAccessGrantPermissionAuthorizer permissions)
{
    public async ValueTask<bool> CanReadRestrictedAsync<T>(IRequestContext<T> context, CancellationToken ct)
        where T : IProviderRequest
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return false;
        var tenantId = context.Request.TenantId;
        var visibility = await permissions.GetProgramVisibilityAsync(tenantId, userId, RbacIds.Member(tenantId, userId),
            RbacPermissions.ProviderInventoryManage, ct).ConfigureAwait(false);
        return visibility.OrganizationWide;
    }
}
