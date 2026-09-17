using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RegisterTenantSlugHandler(IAggregateExecutor executor)
    : IRequestHandler<RegisterTenantSlug>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RegisterTenantSlug> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new TenantSlug(context.Request.Slug),
            slug => AggregateOutcome.CommitOnSuccess(slug.Register(context.Request.TenantId)),
            context, ct);
}
