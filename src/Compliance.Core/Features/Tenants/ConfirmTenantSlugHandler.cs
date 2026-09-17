using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ConfirmTenantSlugHandler(IAggregateExecutor executor) : IRequestHandler<ConfirmTenantSlug>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ConfirmTenantSlug> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.ConfirmSlug(context.Request.Slug)),
            context, ct);
}
