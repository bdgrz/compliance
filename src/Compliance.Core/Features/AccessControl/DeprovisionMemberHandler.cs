using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Terminates a membership episode after preserving another active tenant administrator.</summary>
public sealed class DeprovisionMemberHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock, TenantManagerInvariant managers, MemberAuthorityCleanup cleanup)
    : IRequestHandler<DeprovisionMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DeprovisionMember> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Member deprovisioning requires a Bdgrz user identity."));

        var request = context.Request;
        if (actorUserId == request.UserId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An administrator cannot deprovision their own tenant membership."));

        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        var actorDisplay = UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId);
        var current = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct)
            .ConfigureAwait(false);
        if (!current.IsRegistered && !current.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));

        if (!current.IsDeprovisioned)
        {
            var deprovisionedAt = clock.GetUtcNow();
            var valid = current.Deprovision(actorMemberId, actorDisplay, deprovisionedAt, request.Reason);
            if (!valid.IsSuccess)
                return valid;

            var guarded = await managers.WithdrawAsync(context, request.TenantId, current.Id,
                actorMemberId, TenantManagerInvariant.Deprovisioned, ct).ConfigureAwait(false);
            if (!guarded.IsSuccess)
                return guarded;

            var committed = await executor.ExecuteAsync(new Member(request.TenantId, request.UserId),
                member => AggregateOutcome.CommitOnSuccess(
                    member.Deprovision(actorMemberId, actorDisplay, deprovisionedAt, request.Reason)),
                context, ct).ConfigureAwait(false);
            if (!committed.IsSuccess)
                return committed;

            current = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct)
                .ConfigureAwait(false);
        }

        if (current.IsDeprovisionCleanupComplete)
            return Result.Success;

        return await cleanup.CompleteAsync(context, request.TenantId, request.UserId, current.Id,
            current.DeprovisionedByMemberId, current.DeprovisionedByDisplay!, ct).ConfigureAwait(false);
    }
}
