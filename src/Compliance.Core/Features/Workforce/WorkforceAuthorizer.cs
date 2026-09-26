using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

sealed class WorkforceAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IWorkforceRequest>
{
    // Field restriction (M0-D06) applies once restricted workforce fields exist.
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IWorkforceRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "The workforce roster requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The tenant is not active."));
        return await permissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), RbacPermissions.WorkforceManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not inspect or manage the workforce roster."));
    }
}
