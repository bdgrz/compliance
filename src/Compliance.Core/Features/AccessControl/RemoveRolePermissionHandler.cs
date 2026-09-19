using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RemoveRolePermissionHandler(IAggregateExecutor executor)
    : IRequestHandler<RemoveRolePermission>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveRolePermission> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new RolePermission(context.Request.TenantId, context.Request.RoleId, context.Request.Permission),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove()),
            context, ct);
}
