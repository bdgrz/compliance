using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DeleteTeamHandler(IAggregateExecutor executor) : IRequestHandler<DeleteTeam>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DeleteTeam> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Team(context.Request.TenantId, context.Request.TeamId),
            team => AggregateOutcome.CommitOnSuccess(team.Delete()),
            context, ct);
}
