using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RemoveRolePermissionHandler(IAggregateExecutor executor)
    : IRequestHandler<RemoveRolePermission>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveRolePermission> context, CancellationToken ct) =>
        context.Request.RoleId == BuiltInRbac.TenantAdministrationRoleId(context.Request.TenantId) &&
        context.Request.Permission is RbacPermissions.TenantAccess or RbacPermissions.TenantRbacManage
            ? ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The Tenant Administration role keeps tenant access and RBAC management.")))
            : executor.ExecuteAsync(
            new RolePermission(context.Request.TenantId, context.Request.RoleId, context.Request.Permission),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove()),
            context, ct);
}
