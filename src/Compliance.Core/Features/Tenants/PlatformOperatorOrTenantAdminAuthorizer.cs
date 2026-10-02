using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Tenant-address changes are available to platform operators and to active Org Admins of
///     the target tenant. This does not grant platform-level or business-record access.
/// </summary>
sealed class PlatformOperatorOrTenantAdminAuthorizer(
    IPlatformOperatorAccess operators,
    ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants,
    IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IPlatformOperatorOrTenantAdminRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IPlatformOperatorOrTenantAdminRequest> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Organization address changes require a Bdgrz user identity."));
        if (await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false))
            return Result.Success;

        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.IsSuspended)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The tenant is not active."));

        var memberId = RbacIds.Member(tenantId, userId);
        return await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantRbacManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not manage this tenant's RBAC."));
    }
}
