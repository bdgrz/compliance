using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AssignTeamMemberHandler(IAggregateExecutor executor) : IRequestHandler<AssignTeamMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssignTeamMember> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TeamMember(context.Request.TenantId, context.Request.TeamId, context.Request.MemberId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign()),
            context, ct);
}
