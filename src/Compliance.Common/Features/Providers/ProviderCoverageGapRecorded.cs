using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.coverage_gap.recorded", 1)]
public sealed record ProviderCoverageGapRecorded(Uuid TenantId, Uuid GapId, Uuid ProviderId,
    Uuid RequestId, long ProviderRevision, ProviderCoverageGapContent Content,
    ActorReference Actor, DateTimeOffset RecordedAt) : DomainEvent;
