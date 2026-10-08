using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.coverage_gap.record", 1)]
public sealed record RecordProviderCoverageGap(Uuid TenantId, Uuid ProviderId,
    ProviderCoverageGapContent Content) : IRequest<ProviderCoverageGapRegistration>,
    IProviderManagementRequest, IClientManagementMutationRequest, ICallable;
