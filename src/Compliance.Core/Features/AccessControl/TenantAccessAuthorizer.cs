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
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantAccess, ct)
            .ConfigureAwait(false);
        return allowed
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor may not access this tenant."));
    }
}
