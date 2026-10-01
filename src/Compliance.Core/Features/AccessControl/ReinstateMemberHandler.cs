using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reinstates a member, restoring them in the tenant manager guard first (#432).</summary>
public sealed class ReinstateMemberHandler(IAggregateExecutor executor, TimeProvider clock,
    TenantManagerInvariant managers)
    : IRequestHandler<ReinstateMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReinstateMember> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Member reinstatement requires a Bdgrz user identity."));

        var request = context.Request;
        var actorMemberId = RbacIds.Member(request.TenantId, actorUserId);
        var restored = await managers.RestoreAsync(context, request.TenantId,
            RbacIds.Member(request.TenantId, request.UserId), TenantManagerInvariant.Suspended, ct)
            .ConfigureAwait(false);
        if (!restored.IsSuccess)
            return restored;
        return await executor.ExecuteAsync(new Member(request.TenantId, request.UserId), member =>
                AggregateOutcome.CommitOnSuccess(member.Reinstate(actorMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
