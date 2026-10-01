using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class GetProviderRevisionHandler(ProviderReadConsistency consistency) : IRequestHandler<GetProviderRevision, ProviderView>
{
    public ValueTask<Result<ProviderView>> HandleAsync(IRequestContext<GetProviderRevision> context, CancellationToken ct) =>
        consistency.GetRevisionAsync(context.Request.TenantId, context.Request.ProviderId, context.Request.Revision, ct);
}
