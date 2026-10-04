using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Tenant register operations require current business authority covering the organization.</summary>
sealed class ProviderAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IAccessGrantPermissionAuthorizer permissions) : IRequestAuthorizer<IProviderRequest>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<IProviderRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Deny(RequestErrorKind.Unauthorized, "Provider inventory requires a Bdgrz user identity.");
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null || membership.IsSuspended || membership.IsDeprovisioned ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return Deny(RequestErrorKind.NotFound, "The tenant was not found.");
        if (membership.Affiliation != "client_personnel")
            return Deny(RequestErrorKind.Forbidden, "The actor has no organization provider authority.");
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Deny(RequestErrorKind.Forbidden, "The tenant is not active.");
        var permission = context.Request is IProviderManagementRequest ? RbacPermissions.ProviderInventoryManage : RbacPermissions.TenantAccess;
        var visibility = await permissions.GetProgramVisibilityAsync(tenantId, userId,
            RbacIds.Member(tenantId, userId), permission, ct).ConfigureAwait(false);
        return visibility.OrganizationWide ? Result.Success : Deny(RequestErrorKind.Forbidden,
            "The actor requires organization-wide provider authority.");
    }

    static Result Deny(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
