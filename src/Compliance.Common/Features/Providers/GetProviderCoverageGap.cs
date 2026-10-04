using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.coverage_gap.get", 1)]
public sealed record GetProviderCoverageGap(Uuid TenantId, Uuid ProviderId, Uuid GapId)
    : IRequest<ProviderCoverageGapView>, IProviderRequest, ICallable;
