using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Row-level authorization for RBAC-management commands: reactors bootstrapping a tenant's RBAC
///     run as the trusted system actor and always pass, and any other caller needs
///     <see cref="RbacPermissions.TenantRbacManage" /> in the target tenant.
/// </summary>
sealed class RbacManagementAuthorizer(IPermissionAuthorizer permissions, ITenantActivity tenants,
    ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<IRbacManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IRbacManagementRequest> context,
        CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return context.Request is GrantAccess or RevokeAccessGrant
                ? Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Access grant changes require an authenticated member."))
                : Result.Success;

        return await AuthorizeTenantAsync(context.Actor, context.Request.TenantId, ct).ConfigureAwait(false);
    }

    internal async ValueTask<Result> AuthorizeTenantAsync(ClaimsPrincipal actor, Uuid tenantId, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return Result.Failure(new RequestError(
                RequestErrorKind.Unauthorized,
                "RBAC management requires a Bdgrz user identity."));

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        var memberId = RbacIds.Member(tenantId, userId);
        var allowed = await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantRbacManage, ct)
            .ConfigureAwait(false);
        return allowed
            ? Result.Success
            : Result.Failure(new RequestError(
                RequestErrorKind.Forbidden,
                "The actor may not manage this tenant's RBAC."));
    }
}
