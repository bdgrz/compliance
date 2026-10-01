using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Suspends a member after the tenant manager guard confirms that another active
///     administrator remains (#432). The member decision is checked first, so an invalid request
///     never records a guard withdrawal.
/// </summary>
public sealed class SuspendMemberHandler(IAggregateExecutor executor, TimeProvider clock,
    TenantManagerInvariant managers, IAggregateReader reader)
    : IRequestHandler<SuspendMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<SuspendMember> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Member suspension requires a Bdgrz user identity."));

        var request = context.Request;
        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        if (actorUserId == request.UserId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An administrator cannot suspend their own tenant membership."));

        var actorDisplay = UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId);
        var suspendedAt = clock.GetUtcNow();
        var current = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct)
            .ConfigureAwait(false);
        var wasSuspended = current.IsSuspended;
        var decision = current.Suspend(actorMemberId, actorDisplay, suspendedAt, request.Reason);
        if (!decision.IsSuccess)
            return decision;
        if (!wasSuspended)
        {
            var guarded = await managers.WithdrawAsync(context, request.TenantId,
                RbacIds.Member(request.TenantId, request.UserId), actorMemberId,
                TenantManagerInvariant.Suspended, ct).ConfigureAwait(false);
            if (!guarded.IsSuccess)
                return guarded;
        }

        return await executor.ExecuteAsync(new Member(request.TenantId, request.UserId), member =>
                AggregateOutcome.CommitOnSuccess(member.Suspend(actorMemberId, actorDisplay,
                    suspendedAt, request.Reason)), context, ct).ConfigureAwait(false);
    }
}
