using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

sealed class ProviderWriteAuthorizationProbeHandler : IRequestHandler<RecordProvider, ProviderRegistration>
{
    public ValueTask<Result<ProviderRegistration>> HandleAsync(IRequestContext<RecordProvider> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<ProviderRegistration>.Success(new ProviderRegistration(context.RequestId, 1)));
}
