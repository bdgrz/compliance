using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantOwnerHandler(IAggregateExecutor executor)
    : IRequestHandler<RegisterTenantOwner>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RegisterTenantOwner> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TenantOwner(context.Request.TenantId, context.Request.UserId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Register()),
            context, ct);
}
