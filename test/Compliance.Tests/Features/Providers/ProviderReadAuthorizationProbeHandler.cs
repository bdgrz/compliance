using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

sealed class ProviderReadAuthorizationProbeHandler : IRequestHandler<GetProvider, ProviderView>
{
    public ValueTask<Result<ProviderView>> HandleAsync(IRequestContext<GetProvider> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<ProviderView>.Success(new ProviderView(context.Request.TenantId,
            context.Request.ProviderId, 1, new ProviderContent("Provider", "Supplier"), "manual", "active", [],
            ActorReference.ForSystemProcess("probe", "Probe"), DateTimeOffset.UtcNow)));
}
