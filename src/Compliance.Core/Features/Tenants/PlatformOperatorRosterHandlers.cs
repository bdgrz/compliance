using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class SeedPlatformOperatorRosterHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SeedPlatformOperatorRoster> context,
        CancellationToken ct) =>
        executor.ExecuteAsync(new PlatformOperatorRoster(),
            roster => AggregateOutcome.CommitOnSuccess(roster.Seed(context.Request.UserIds,
                clock.GetUtcNow())), context, ct);
}

sealed class SeedPlatformOperatorRosterAuthorizer : IRequestAuthorizer<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<SeedPlatformOperatorRoster> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the system may seed platform operators.")));
}

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

public sealed class ListPlatformOperatorsHandler(IAggregateReader reader)
    : IRequestHandler<ListPlatformOperators, PlatformOperatorRosterView>
{
    public async ValueTask<Result<PlatformOperatorRosterView>> HandleAsync(
        IRequestContext<ListPlatformOperators> context, CancellationToken ct)
    {
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), ct).ConfigureAwait(false);
        return Result<PlatformOperatorRosterView>.Success(new PlatformOperatorRosterView(roster.ActiveOperators));
    }
}
