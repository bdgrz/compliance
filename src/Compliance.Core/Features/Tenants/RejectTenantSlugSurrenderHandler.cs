using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RejectTenantSlugSurrenderHandler(IAggregateExecutor executor)
    : IRequestHandler<RejectTenantSlugSurrender>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<RejectTenantSlugSurrender> context,
        CancellationToken ct) =>
        executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.RejectSlugSurrender(context.Request.Slug)),
            context, ct);
}
