using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DefineRoleHandler(IAggregateExecutor executor) : IRequestHandler<DefineRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DefineRole> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Role(context.Request.TenantId, context.Request.RoleId),
            role => AggregateOutcome.CommitOnSuccess(role.Define(context.Request.Name)),
            context, ct);
}
