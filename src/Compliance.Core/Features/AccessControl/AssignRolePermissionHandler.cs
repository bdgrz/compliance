using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignRolePermissionHandler(IAggregateExecutor executor)
    : IRequestHandler<AssignRolePermission>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssignRolePermission> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new RolePermission(context.Request.TenantId, context.Request.RoleId, context.Request.Permission),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign()),
            context, ct);
}
