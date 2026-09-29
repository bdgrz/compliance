using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class SuspendMemberHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<SuspendMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SuspendMember> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Member suspension requires a Bdgrz user identity.")));

        var actorMemberId = RbacIds.Member(context.Request.TenantId, actorUserId);
        if (actorUserId == context.Request.UserId)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An administrator cannot suspend their own tenant membership.")));

        return executor.ExecuteAsync(new Member(context.Request.TenantId, context.Request.UserId), member =>
                AggregateOutcome.CommitOnSuccess(member.Suspend(actorMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId), clock.GetUtcNow(),
                    context.Request.Reason)), context, ct);
    }
}
