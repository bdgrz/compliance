using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplicationInventoryAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IApplicationInventoryRequest>
{
    // A narrow V1 grant-backfill checkpoint replays TenantRegistered without replaying broader
    // tenant bootstrap side effects. Record-level policy remains a later authority decision.
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IApplicationInventoryRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Application inventory requires a Bdgrz user identity."));
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
                RbacIds.Member(tenantId, userId), RbacPermissions.ApplicationInventoryManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not inspect or manage this inventory."));
    }
}
