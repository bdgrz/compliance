using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class SeparationOfDutiesWaiverAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<ISeparationOfDutiesWaiverAdminRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<ISeparationOfDutiesWaiverAdminRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Waiver administration requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to administer client waivers."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The tenant is not active."));
        return await permissions.IsAllowedAsync(tenantId, userId, RbacIds.Member(tenantId, userId),
                RbacPermissions.TenantRbacManage, ct).ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not manage this tenant's waivers."));
    }
}
