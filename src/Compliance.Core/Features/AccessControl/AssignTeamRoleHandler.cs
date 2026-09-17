using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignTeamRoleHandler(IAggregateExecutor executor) : IRequestHandler<AssignTeamRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssignTeamRole> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TeamRole(context.Request.TenantId, context.Request.TeamId, context.Request.RoleId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign()),
            context, ct);
}
