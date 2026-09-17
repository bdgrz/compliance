using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class DefineTeamHandler(IAggregateExecutor executor) : IRequestHandler<DefineTeam>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DefineTeam> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Team(context.Request.TenantId, context.Request.TeamId),
            team => AggregateOutcome.CommitOnSuccess(team.Define(context.Request.Name)),
            context, ct);
}
