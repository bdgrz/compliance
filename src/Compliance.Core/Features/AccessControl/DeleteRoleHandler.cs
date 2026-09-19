using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DeleteRoleHandler(IAggregateExecutor executor) : IRequestHandler<DeleteRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DeleteRole> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Role(context.Request.TenantId, context.Request.RoleId),
            role => AggregateOutcome.CommitOnSuccess(role.Delete()),
            context, ct);
}
