using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Requires the client's active member and ordinary manager grant; operators get no client authority.</summary>
sealed class IndependenceAdministrationAuthorizer(IPermissionAuthorizer permissions,
    ITenantActivity tenants, ITenantMembershipDirectoryReader memberships)
    : IRequestAuthorizer<IIndependenceAdministrationRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IIndependenceAdministrationRequest> context,
        CancellationToken ct) => new RbacManagementAuthorizer(permissions, tenants, memberships)
        .AuthorizeTenantAsync(context.Actor, context.Request.TenantId, ct);
}
