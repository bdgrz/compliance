using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ConfirmTenantSlugSurrenderHandler(IAggregateExecutor executor)
    : IRequestHandler<ConfirmTenantSlugSurrender>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<ConfirmTenantSlugSurrender> context,
        CancellationToken ct) =>
        executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.ConfirmSlugSurrender(context.Request.Slug)),
            context, ct);
}
