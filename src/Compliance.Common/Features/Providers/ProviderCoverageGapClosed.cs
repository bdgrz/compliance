using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.coverage_gap.closed", 1)]
public sealed record ProviderCoverageGapClosed(Uuid TenantId, Uuid GapId, Uuid ProviderId,
    Uuid RequestId, long Revision, ProviderCoverageGapClosureContent Content,
    ActorReference Actor, DateTimeOffset ClosedAt) : DomainEvent;
