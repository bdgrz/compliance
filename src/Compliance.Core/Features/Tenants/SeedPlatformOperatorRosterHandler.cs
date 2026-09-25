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
