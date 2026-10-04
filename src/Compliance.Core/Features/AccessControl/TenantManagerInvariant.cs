using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Preserves at least one active tenant administrator (#432). An active administrator is a
///     registered, unsuspended member assigned to the built-in Administrators team, whose
///     undeletable Tenant Administration role keeps <c>tenant.access</c> and
///     <c>tenant.rbac.manage</c>, and whom the <see cref="TenantManagerGuard" /> has not
///     withdrawn. Member suspension and removal from the Administrators team are serialized
///     through the guard and need another active administrator as a witness, read from source
///     streams rather than lagging projections. Reinstatement and reassignment restore the
///     member in the guard first. Removing that role from the team or those permissions from the
///     role is refused outright. Suspension and deprovisioning withdraw a member through this
///     invariant before changing membership.
/// </summary>
public sealed class TenantManagerInvariant(IAggregateReader reader, IAggregateExecutor executor,
    ITeamMemberDirectoryReader teamMembers)
{
    public const string Suspended = "suspended";
    public const string Deprovisioned = "deprovisioned";
    public const string RemovedFromAdministrators = "administrators_team_removed";
    const int MaximumAttempts = 8;

    /// <summary>
    ///     Serializes a decision that withdraws <paramref name="memberId" />. It fails with a
    ///     non-transient conflict when no other active administrator would remain.
    /// </summary>
    public async ValueTask<Result> WithdrawAsync(IExecutionContext context, Uuid tenantId,
        Uuid memberId, Uuid? actorMemberId, string reason, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var guard = await reader.HydrateAsync(new TenantManagerGuard(tenantId), ct)
                .ConfigureAwait(false);
            var observed = guard.Sequence;
            var remains = !await IsActiveAdministratorAsync(guard, tenantId, memberId, ct)
                              .ConfigureAwait(false) ||
                          await HasOtherAdministratorAsync(guard, tenantId, memberId, actorMemberId, ct)
                              .ConfigureAwait(false);
            try
            {
                var result = await executor.ExecuteAsync(new TenantManagerGuard(tenantId),
                    current => AggregateOutcome.CommitOnSuccess(
                        current.Withdraw(memberId, reason, observed, remains)), context, ct)
                    .ConfigureAwait(false);
                if (result.IsSuccess || !result.Error.IsTransient)
                    return result;
            }
            catch (EventStreamConcurrencyException)
            {
                // Another guard decision committed between hydration and append; decide again.
            }
        }
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "Other tenant manager changes are in progress; retry.", isTransient: true));
    }

    /// <summary>Clears a withdrawal before the source change that reverses it commits.</summary>
    public async ValueTask<Result> RestoreAsync(IExecutionContext context, Uuid tenantId,
        Uuid memberId, string reason, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new TenantManagerGuard(tenantId),
                    current => AggregateOutcome.CommitOnSuccess(current.Restore(memberId, reason)),
                    context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException)
            {
                // Another guard decision committed first; restore against the newer state.
            }
        }
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "Other tenant manager changes are in progress; retry.", isTransient: true));
    }

    async ValueTask<bool> HasOtherAdministratorAsync(TenantManagerGuard guard, Uuid tenantId,
        Uuid memberId, Uuid? actorMemberId, CancellationToken ct)
    {
        if (actorMemberId is { } actor && actor != memberId &&
            await IsActiveAdministratorAsync(guard, tenantId, actor, ct).ConfigureAwait(false))
            return true;
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        string? cursor = null;
        do
        {
            var page = await teamMembers.ListAsync(tenantId, teamId, 200, cursor, null, false, ct)
                .ConfigureAwait(false);
            foreach (var candidate in page.Items)
            {
                if (candidate.MemberId != memberId && candidate.MemberId != actorMemberId &&
                    await IsActiveAdministratorAsync(guard, tenantId, candidate.MemberId, ct)
                        .ConfigureAwait(false))
                    return true;
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return false;
    }

    async ValueTask<bool> IsActiveAdministratorAsync(TenantManagerGuard guard, Uuid tenantId,
        Uuid memberId, CancellationToken ct)
    {
        if (guard.IsWithdrawn(memberId))
            return false;
        var assignment = await reader.HydrateAsync(new TeamMember(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), memberId), ct).ConfigureAwait(false);
        if (!assignment.IsAssigned)
            return false;
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended &&
               assignment.MembershipEpisodeId == member.MembershipEpisodeId;
    }
}
