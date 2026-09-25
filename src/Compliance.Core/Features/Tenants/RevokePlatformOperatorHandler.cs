using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RevokePlatformOperatorHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevokePlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokePlatformOperator> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated platform user is required.")));
        return executor.ExecuteAsync(new PlatformOperatorRoster(),
            roster => AggregateOutcome.CommitOnSuccess(roster.Revoke(actorUserId,
                context.Request.UserId, context.Request.Reason, clock.GetUtcNow(),
                UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId))), context, ct);
    }
}
