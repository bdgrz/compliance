using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Row-level authorization for RBAC-management commands: reactors bootstrapping a tenant's RBAC
///     run as the trusted system actor and always pass, and any other caller needs
///     <see cref="RbacPermissions.TenantRbacManage" /> in the target tenant.
/// </summary>
sealed class RbacManagementAuthorizer(IPermissionAuthorizer permissions, ITenantActivity tenants) : IRequestAuthorizer<IRbacManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IRbacManagementRequest> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;

        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "RBAC management requires a Bdgrz user identity."));

        var tenantId = context.Request.TenantId;
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        var allowed = await permissions.IsAllowedAsync(tenantId, memberId, RbacPermissions.TenantRbacManage, ct)
            .ConfigureAwait(false);
        return allowed
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor may not manage this tenant's RBAC."));
    }
}
