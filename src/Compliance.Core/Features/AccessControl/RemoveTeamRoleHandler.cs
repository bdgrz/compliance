using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RemoveTeamRoleHandler(IAggregateExecutor executor) : IRequestHandler<RemoveTeamRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveTeamRole> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TeamRole(context.Request.TenantId, context.Request.TeamId, context.Request.RoleId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove()),
            context, ct);
}
