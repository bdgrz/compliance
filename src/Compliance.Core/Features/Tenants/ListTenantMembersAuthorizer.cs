using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Member directories require platform metadata authority or tenant RBAC management.</summary>
sealed class ListTenantMembersAuthorizer(IPlatformOperatorAccess operators, IPermissionAuthorizer permissions,
    ITenantActivity tenants, ITenantMembershipDirectoryReader memberships) : IRequestAuthorizer<ListTenantMembers>
{
    readonly RbacManagementAuthorizer _management = new(permissions, tenants, memberships);

    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ListTenantMembers> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return Result.Success;
        if (UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) &&
            await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false))
            return Result.Success;
        return await _management.AuthorizeTenantAsync(context.Actor, context.Request.TenantId, ct).ConfigureAwait(false);
    }
}
