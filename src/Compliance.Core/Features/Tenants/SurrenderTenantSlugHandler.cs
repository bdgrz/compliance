using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class SurrenderTenantSlugHandler(IAggregateExecutor executor)
    : IRequestHandler<SurrenderTenantSlug>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SurrenderTenantSlug> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TenantSlug(context.Request.Slug),
            slug => AggregateOutcome.CommitOnSuccess(slug.Surrender(context.Request.TenantId)),
            context, ct);
}
