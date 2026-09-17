using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RequestTenantSlugSurrenderHandler(IAggregateExecutor executor)
    : IRequestHandler<RequestTenantSlugSurrender>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<RequestTenantSlugSurrender> context,
        CancellationToken ct) =>
        executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.RequestSlugSurrender(context.Request.Slug)),
            context, ct);
}
