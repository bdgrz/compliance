using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

sealed class TechnologyInventoryAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions,
    IAccessGrantScopePermissionAuthorizer? scopedPermissions = null)
    : IRequestAuthorizer<ITechnologyInventoryRequest>
{
    // Technology inventory has its own permission so its scoped authorization can evolve
    // independently from the application inventory.
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<ITechnologyInventoryRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "The technology inventory requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        if (await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TechnologyInventoryManage, ct).ConfigureAwait(false))
            return Result.Success;
        if (context.Request is ITechnologyInventoryReadRequest && scopedPermissions is not null &&
            await scopedPermissions.IsAllowedAtAnyTechnologyInventoryScopeAsync(tenantId,
                userId, memberId, RbacPermissions.TechnologyInventoryManage, ct)
                .ConfigureAwait(false))
            return Result.Success;
        return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The actor may not inspect or manage the technology inventory."));
    }
}
