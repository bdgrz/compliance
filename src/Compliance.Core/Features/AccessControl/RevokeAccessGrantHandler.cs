using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RevokeAccessGrantHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevokeAccessGrant>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokeAccessGrant> context, CancellationToken ct)
    {
        var request = context.Request;
        return executor.ExecuteAsync(new AccessGrant(request.TenantId, request.GrantId),
            grant => AggregateOutcome.CommitOnSuccess(grant.Revoke(
                AccessGrantActor.From(context, request.TenantId), clock.GetUtcNow())), context, ct);
    }
}
