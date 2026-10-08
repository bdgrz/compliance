using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Closes an open provider gap against newer or different coverage evidence.</summary>
[Discriminator("bdgrz.provider.coverage_gap.close", 1)]
public sealed record CloseProviderCoverageGap(Uuid TenantId, Uuid ProviderId, Uuid GapId,
    long ExpectedRevision, ProviderCoverageGapClosureContent Content)
    : IRequest<ProviderCoverageGapRegistration>, IProviderManagementRequest, IClientManagementMutationRequest, ICallable;
