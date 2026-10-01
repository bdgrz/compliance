using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Assigns a team member. Reassignment to the built-in Administrators team restores the
///     member in the tenant manager guard first (#432).
/// </summary>
public sealed class AssignTeamMemberHandler(IAggregateExecutor executor,
    TenantManagerInvariant managers) : IRequestHandler<AssignTeamMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId))
        {
            var restored = await managers.RestoreAsync(context, request.TenantId, request.MemberId,
                TenantManagerInvariant.RemovedFromAdministrators, ct).ConfigureAwait(false);
            if (!restored.IsSuccess)
                return restored;
        }
        return await executor.ExecuteAsync(
            new TeamMember(request.TenantId, request.TeamId, request.MemberId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign()),
            context, ct).ConfigureAwait(false);
    }
}
