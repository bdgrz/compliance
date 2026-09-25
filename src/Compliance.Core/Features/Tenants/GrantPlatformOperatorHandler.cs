using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class GrantPlatformOperatorHandler(IAggregateExecutor executor,
    IPlatformUserDirectoryReader users, TimeProvider clock)
    : IRequestHandler<GrantPlatformOperator>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<GrantPlatformOperator> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated platform user is required."));
        if (context.Request.UserId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A platform user is required."));
        if (!await users.ExistsAsync(context.Request.UserId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The platform user is not available yet.", isTransient: true));
        return await executor.ExecuteAsync(new PlatformOperatorRoster(),
                roster => AggregateOutcome.CommitOnSuccess(roster.Grant(actorUserId,
                    context.Request.UserId, context.Request.Reason, clock.GetUtcNow(),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, actorUserId))), context, ct)
            .ConfigureAwait(false);
    }
}
