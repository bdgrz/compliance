using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RejectTenantSlugHandler(IAggregateExecutor executor) : IRequestHandler<RejectTenantSlug>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RejectTenantSlug> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.RejectSlug(context.Request.Slug)),
            context, ct);
}
