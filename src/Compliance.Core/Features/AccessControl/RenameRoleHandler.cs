using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RenameRoleHandler(IAggregateExecutor executor) : IRequestHandler<RenameRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RenameRole> context, CancellationToken ct)
    {
        if (!BuiltInRbac.IsBuiltInRole(context.Request.TenantId, context.Request.RoleId) ||
            !RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the system may migrate built-in role names.")));

        return executor.ExecuteAsync(new Role(context.Request.TenantId, context.Request.RoleId),
            role => AggregateOutcome.CommitOnSuccess(role.Rename(context.Request.Name)), context, ct);
    }
}
