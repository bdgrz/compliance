using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RemoveTeamMemberHandler(IAggregateExecutor executor) : IRequestHandler<RemoveTeamMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveTeamMember> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TeamMember(context.Request.TenantId, context.Request.TeamId, context.Request.MemberId),
            teamMember => AggregateOutcome.CommitOnSuccess(teamMember.Remove()),
            context, ct);
}
