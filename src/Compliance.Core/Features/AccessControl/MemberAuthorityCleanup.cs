using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes tenant RBAC and direct scoped grants from a terminated membership episode.</summary>
public sealed class MemberAuthorityCleanup(IAggregateReader reader, IAggregateExecutor executor,
    ITeamMemberDirectoryReader teamMembers, IAccessGrantDirectory grants, TimeProvider clock)
{
    const int MaximumAttempts = 5;

    internal async ValueTask<Result> CompleteAsync(IExecutionContext context, Uuid tenantId,
        Uuid userId, Uuid memberId, Uuid actorMemberId, string actorDisplay, CancellationToken ct)
    {
        var actor = ActorReference.ForMember(actorMemberId, actorDisplay);
        var assignments = await teamMembers.ListMemberAssignmentsAsync(tenantId, memberId, ct)
            .ConfigureAwait(false);
        foreach (var assignment in assignments)
        {
            var removed = await RemoveTeamAssignmentAsync(context, tenantId, assignment.TeamId,
                memberId, ct).ConfigureAwait(false);
            if (!removed.IsSuccess)
                return removed;
        }

        var grantSet = await grants.ListAsync(tenantId, ct).ConfigureAwait(false);
        foreach (var grant in grantSet.Grants.Where(item => item.RevokedAt is null &&
                     item.Terms.Principal.Kind == AccessGrantPrincipalKind.Member &&
                     item.Terms.Principal.Id == memberId))
        {
            var revoked = await RevokeGrantAsync(context, tenantId, grant.GrantId, actor, ct)
                .ConfigureAwait(false);
            if (!revoked.IsSuccess)
                return revoked;
        }

        return await executor.ExecuteAsync(new Member(tenantId, userId),
            member => AggregateOutcome.CommitOnSuccess(
                member.CompleteDeprovisionCleanup(clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result> RemoveTeamAssignmentAsync(IExecutionContext context, Uuid tenantId,
        Uuid teamId, Uuid memberId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var assignment = await reader.HydrateAsync(new TeamMember(tenantId, teamId, memberId), ct)
                .ConfigureAwait(false);
            if (!assignment.IsAssigned)
                return Result.Success;
            try
            {
                var result = await executor.ExecuteAsync(new TeamMember(tenantId, teamId, memberId),
                    current => AggregateOutcome.CommitOnSuccess(current.Remove()), context, ct)
                    .ConfigureAwait(false);
                if (result.IsSuccess || result.Error is RequestError { Kind: RequestErrorKind.NotFound })
                    return Result.Success;
                return result;
            }
            catch (EventStreamConcurrencyException) when (attempt + 1 < MaximumAttempts)
            {
                // Re-read and retry an assignment that changed during cleanup.
            }
        }
        return RetryConflict();
    }

    async ValueTask<Result> RevokeGrantAsync(IExecutionContext context, Uuid tenantId,
        Uuid grantId, ActorReference actor, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var grant = await reader.HydrateAsync(new AccessGrant(tenantId, grantId), ct)
                .ConfigureAwait(false);
            if (grant.IsRevoked)
                return Result.Success;
            try
            {
                var result = await executor.ExecuteAsync(new AccessGrant(tenantId, grantId),
                    current => AggregateOutcome.CommitOnSuccess(current.Revoke(actor, clock.GetUtcNow())),
                    context, ct).ConfigureAwait(false);
                if (result.IsSuccess || result.Error is RequestError { Kind: RequestErrorKind.Conflict })
                    return Result.Success;
                return result;
            }
            catch (EventStreamConcurrencyException) when (attempt + 1 < MaximumAttempts)
            {
                // Re-read and retry a grant that changed during cleanup.
            }
        }
        return RetryConflict();
    }

    static Result RetryConflict() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Membership authority changed during deprovisioning; retry.", isTransient: true));
}
