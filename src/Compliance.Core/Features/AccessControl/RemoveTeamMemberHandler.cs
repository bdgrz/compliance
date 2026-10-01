using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a team member. Removal from the built-in Administrators team first needs the
///     tenant manager guard to confirm another active administrator remains (#432).
/// </summary>
public sealed class RemoveTeamMemberHandler(IAggregateExecutor executor,
    TenantManagerInvariant managers) : IRequestHandler<RemoveTeamMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RemoveTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId))
        {
            Uuid? actorMemberId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId)
                ? RbacIds.Member(request.TenantId, actorUserId)
                : null;
            var guarded = await managers.WithdrawAsync(context, request.TenantId, request.MemberId,
                actorMemberId, TenantManagerInvariant.RemovedFromAdministrators, ct)
                .ConfigureAwait(false);
            if (!guarded.IsSuccess)
                return guarded;
        }
        return await executor.ExecuteAsync(
            new TeamMember(request.TenantId, request.TeamId, request.MemberId),
            teamMember => AggregateOutcome.CommitOnSuccess(teamMember.Remove()),
            context, ct).ConfigureAwait(false);
    }
}
