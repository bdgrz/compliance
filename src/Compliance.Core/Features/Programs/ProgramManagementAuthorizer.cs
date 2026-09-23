using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class ProgramManagementAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IProgramManagementRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProgramManagementRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Program administration requires a Bdgrz user identity."));
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
        return await permissions.IsAllowedAsync(tenantId, memberId, RbacPermissions.ProgramManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not administer this program."));
    }
}
