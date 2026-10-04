using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Assigns a team member. Reassignment to the built-in Administrators team restores the
///     member in the tenant manager guard first (#432).
/// </summary>
public sealed class AssignTeamMemberHandler(IAggregateExecutor executor,
    TenantManagerInvariant managers, IAggregateReader reader) : IRequestHandler<AssignTeamMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        if (!member.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));

        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId))
        {
            foreach (var reason in new[]
                     {
                         TenantManagerInvariant.Suspended,
                         TenantManagerInvariant.Deprovisioned,
                         TenantManagerInvariant.RemovedFromAdministrators,
                     })
            {
                var restored = await managers.RestoreAsync(context, request.TenantId,
                    request.MemberId, reason, ct).ConfigureAwait(false);
                if (!restored.IsSuccess)
                    return restored;
            }
        }
        var membershipEpisodeId = member.MembershipEpisodeId;
        var result = await executor.ExecuteAsync(
            new TeamMember(request.TenantId, request.TeamId, request.MemberId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign(membershipEpisodeId)),
            context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;

        var currentMember = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        if (currentMember.IsRegistered && currentMember.MembershipEpisodeId == membershipEpisodeId)
            return Result.Success;

        var currentAssignment = await reader.HydrateAsync(new TeamMember(request.TenantId,
            request.TeamId, request.MemberId), ct).ConfigureAwait(false);
        if (currentAssignment.IsAssigned && currentAssignment.MembershipEpisodeId == membershipEpisodeId)
            await executor.ExecuteAsync(new TeamMember(request.TenantId, request.TeamId,
                    request.MemberId),
                assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove()), context, ct)
                .ConfigureAwait(false);
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The tenant member changed before the team assignment completed."));
    }
}
