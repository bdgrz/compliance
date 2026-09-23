using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ActivateTenantHandler(IAggregateExecutor executor, IAggregateReader reader,
    ITenantMembershipDirectoryReader memberships, IPermissionAuthorizer permissions)
    : IRequestHandler<ActivateTenant>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ActivateTenant> context, CancellationToken ct)
    {
        var tenantId = context.Request.TenantId;
        var userId = context.Request.FirstAdministratorUserId;
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        // A losing concurrent slug claim is terminal. A successful reaction lets the
        // global bootstrap reactor checkpoint and continue to later registrations.
        if (tenant.IsRegistrationRejected)
            return Result.Success;

        var memberId = RbacIds.Member(tenantId, userId);
        var member = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        var assignment = await reader.HydrateAsync(new TeamMember(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), memberId), ct).ConfigureAwait(false);
        if (!member.IsRegistered || member.Affiliation != "client_personnel" || !assignment.IsAssigned ||
            !await memberships.IsMemberAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false) ||
            !await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantAccess, ct)
                .ConfigureAwait(false) ||
            !await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.TenantRbacManage, ct)
                .ConfigureAwait(false) ||
            !await permissions.IsAllowedAsync(tenantId, userId, memberId,
                RbacPermissions.ProgramManage, ct)
                .ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The first administrator is still being provisioned.", isTransient: true));

        return await executor.ExecuteAsync(new Tenant(tenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.Activate(context.Request.FirstAdministratorUserId,
                context.Request.FirstAdministratorEmail)),
            context, ct).ConfigureAwait(false);
    }
}
