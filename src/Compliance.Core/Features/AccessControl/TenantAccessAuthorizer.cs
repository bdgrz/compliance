using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Row-level authorization for tenant-scoped reads: requires <see cref="RbacPermissions.TenantAccess" />.</summary>
sealed class TenantAccessAuthorizer(IPermissionAuthorizer permissions, ITenantActivity tenants,
    ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<ITenantAccessRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ITenantAccessRequest> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "Tenant access requires a Bdgrz user identity."));

        var tenantId = context.Request.TenantId;
        if (!await memberships.IsMemberAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        var allowed = await permissions.IsAllowedAsync(tenantId, memberId, RbacPermissions.TenantAccess, ct)
            .ConfigureAwait(false);
        return allowed
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor may not access this tenant."));
    }
}
