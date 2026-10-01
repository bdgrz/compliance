using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Requires an active, non-firm tenant member. Management requests also require the
///     access-review permission; participant requests are narrowed to the reviewer's assignments
///     by their handlers.
/// </summary>
sealed class AccessReviewAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IAccessReviewRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IAccessReviewRequest> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Access reviews require a Bdgrz user identity."));
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
        if (context.Request is IAccessReviewParticipantRequest)
            return Result.Success;
        return await permissions.IsAllowedAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), RbacPermissions.AccessReviewManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not inspect or manage access reviews."));
    }
}
