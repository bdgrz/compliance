using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ReinstateMemberHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReinstateMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReinstateMember> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Member reinstatement requires a Bdgrz user identity.")));

        var actorMemberId = RbacIds.Member(context.Request.TenantId, actorUserId);
        return executor.ExecuteAsync(new Member(context.Request.TenantId, context.Request.UserId), member =>
                AggregateOutcome.CommitOnSuccess(member.Reinstate(actorMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId), clock.GetUtcNow())),
            context, ct);
    }
}
