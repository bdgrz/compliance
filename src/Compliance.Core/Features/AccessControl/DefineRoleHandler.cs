using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DefineRoleHandler(IAggregateExecutor executor) : IRequestHandler<DefineRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DefineRole> context, CancellationToken ct)
    {
        if (!BuiltInRbac.IsBuiltInRole(context.Request.TenantId, context.Request.RoleId) ||
            !RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The role catalog is fixed and managed by the system.")));

        return executor.ExecuteAsync(
            new Role(context.Request.TenantId, context.Request.RoleId),
            role => AggregateOutcome.CommitOnSuccess(role.Define(context.Request.Name)),
            context, ct);
    }
}
